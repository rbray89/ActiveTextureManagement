/* -----------------------------------------------------------------------------

        Copyright (c) 2006 Simon Brown                          si@sjbrown.co.uk
        Copyright (c) 2007 Ignacio Castano                   icastano@nvidia.com

        Permission is hereby granted, free of charge, to any person obtaining
        a copy of this software and associated documentation files (the 
        "Software"), to deal in the Software without restriction, including
        without limitation the rights to use, copy, modify, merge, publish,
        distribute, sublicense, and/or sell copies of the Software, and to 
        permit persons to whom the Software is furnished to do so, subject to 
        the following conditions:

        The above copyright notice and this permission notice shall be included
        in all copies or substantial portions of the Software.

        THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
        OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF 
        MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
        IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY 
        CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, 
        TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE 
        SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
        
   -------------------------------------------------------------------------- */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace LibSquishPort
{
    class ClusterFit : ColourFit
    {

        const int kMaxIterations = 8;

        int m_iterationCount;
        Vector3 m_principle;
        byte[] m_order = new byte[16 * kMaxIterations];
        Vector4[] m_points_weights = new Vector4[16];
        Vector4 m_xsum_wsum;
        Vector4 m_metric;
        Vector4 m_besterror;
        static Vector4 grid = new Vector4(31.0f, 63.0f, 31.0f, 0);
        static Vector4 gridrcp = new Vector4(1.0f / 31.0f, 1.0f / 63.0f, 1.0f / 31.0f, 0.0f);

        private static void swap<T>(ref T a, ref T b)
{
 	T atmp = a;
    a = b;
    b = atmp;
}

       public ClusterFit( ColourSet colours, SquishFlags flags ) 
  : base( colours, flags )
{
	// set the iteration count
	m_iterationCount = ( (m_flags & SquishFlags.kColourIterativeClusterFit) != 0 ) ? kMaxIterations : 1;

	// initialise the best error
	m_besterror = Vector4.one* float.MaxValue;

	// initialise the metric
	bool perceptual = ( ( m_flags & SquishFlags.kColourMetricPerceptual ) != 0 );
	if( perceptual )
		m_metric = new Vector4( 0.2126f, 0.7152f, 0.0722f, 0.0f );
	else
		m_metric = Vector4.one;	

	// cache some values
	int count = m_colours.GetCount();
	Vector3[] values = m_colours.GetPoints();

	// get the covariance matrix
	Sym3x3 covariance = math.ComputeWeightedCovariance( count, values, m_colours.GetWeights() );
	
	// compute the principle component
	m_principle = math.ComputePrincipleComponent( covariance );
}

unsafe bool ructOrdering( Vector3 axis, int iteration )
{
	// cache some values
	int count = m_colours.GetCount();
	Vector3[] values = m_colours.GetPoints();

	// build the list of dot products
	float[] dps = new float[16];
    fixed( byte* porder = m_order)
    {
	    byte* order = porder + 16*iteration;
	    for( int i = 0; i < count; ++i )
	    {
		    dps[i] = Vector3.Dot( values[i], axis );
		    order[i] = ( byte )i;
	    }
		
	    // stable sort using them
	    for( int i = 0; i < count; ++i )
	    {
		    for( int j = i; j > 0 && dps[j] < dps[j - 1]; --j )
		    {
			    swap( ref dps[j], ref dps[j - 1] );
			    swap( ref order[j], ref order[j - 1] );
		    }
	    }
	
	    // check this ordering is unique
	    for( int it = 0; it < iteration; ++it )
	    {
		    byte* prev = porder + 16*it;
		    bool same = true;
		    for( int i = 0; i < count; ++i )
		    {
			    if( order[i] != prev[i] )
			    {
				    same = false;
				    break;
			    }
		    }
            
		    if( same )
			    return false;
	    }
	
	    // copy the ordering and weight all the points
	    Vector3[] unweighted = m_colours.GetPoints();
	    float[] weights = m_colours.GetWeights();
	    m_xsum_wsum = Vector4.zero;
	    for( int i = 0; i < count; ++i )
	    {
		    int j = order[i];
		    Vector4 p = new Vector4( unweighted[j].x, unweighted[j].y, unweighted[j].z, 1.0f );
		    Vector4 x = p*weights[j];
		    m_points_weights[i] = x;
		    m_xsum_wsum += x;
	    }
    }
	return true;
}

public unsafe override void Compress3( byte* block )
{
	// declare variables
	int count = m_colours.GetCount();
	Vector4  two = Vector4.one*( 2.0f );
	Vector4  half_half2 = new Vector4( 0.5f, 0.5f, 0.5f, 0.25f );
	Vector4  half = Vector4.one*( 0.5f );

	// prepare an ordering using the principle axis
	ructOrdering( m_principle, 0 );
	
	// check all possible clusters and iterate on the total order
	Vector4 beststart = Vector4.zero;
	Vector4 bestend = Vector4.zero;
	Vector4 besterror = m_besterror;
	byte[] bestindices = new byte[16];
	int bestiteration = 0;
	int besti = 0, bestj = 0;
	
	// loop over iterations (we avoid the case that all points in first or last cluster)
	for( int iterationIndex = 0;; )
	{
		// first cluster [0,i) is at the start
		Vector4 part0 = Vector4.zero;
		for( int i = 0; i < count; ++i )
		{
			// second cluster [i,j) is half along
			Vector4 part1 = ( i == 0 ) ? m_points_weights[0] : Vector4.zero;
			int jmin = ( i == 0 ) ? 1 : i;
			for( int j = jmin;; )
			{
				// last cluster [j,count) is at the end
				Vector4 part2 = m_xsum_wsum - part1 - part0;
				
				// compute least squares terms directly
				Vector4 alphax_sum = part1.MultiplyAdd( half_half2, part0 );
				Vector4 alpha2_sum = alphax_sum.SplatW();

				Vector4 betax_sum = part1.MultiplyAdd( half_half2, part2 );
				Vector4 beta2_sum = betax_sum.SplatW();

				Vector4 alphabeta_sum = Vector4.Scale( part1,half_half2 ).SplatW();

				// compute the least-squares optimal points
				Vector4 factor = ( alphabeta_sum.NegativeMultiplySubtract( alphabeta_sum, Vector4.Scale(alpha2_sum, beta2_sum ) ) ).Reciprocal();
				Vector4 a = Vector4.Scale( betax_sum.NegativeMultiplySubtract( alphabeta_sum, Vector4.Scale(alphax_sum,beta2_sum) ), factor);
				Vector4 b = Vector4.Scale( alphax_sum.NegativeMultiplySubtract( alphabeta_sum, Vector4.Scale(betax_sum,alpha2_sum) ), factor);

				// clamp to the grid
                a = Vector4.Min(Vector4.one, Vector4.Max(Vector4.zero, a));
                b = Vector4.Min(Vector4.one, Vector4.Max(Vector4.zero, b));
				a = Vector4.Scale(( grid.MultiplyAdd( a, half ) ).Truncate(),gridrcp);
				b = Vector4.Scale(( grid.MultiplyAdd( b, half ) ).Truncate(),gridrcp);
				
				// compute the error (we skip the ant xxsum)
				Vector4 e1 = Vector4.Scale(a,a).MultiplyAdd( alpha2_sum, Vector4.Scale(Vector4.Scale(b,b),beta2_sum) );
				Vector4 e2 = a.NegativeMultiplySubtract( alphax_sum, Vector4.Scale(Vector4.Scale(a,b),alphabeta_sum) );
				Vector4 e3 = b.NegativeMultiplySubtract( betax_sum, e2 );
				Vector4 e4 = two.MultiplyAdd( e3, e1 );

				// apply the metric to the error term
				Vector4 e5 = Vector4.Scale(e4,m_metric);
				Vector4 error = e5.SplatX() + e5.SplatY() + e5.SplatZ();
				
				// keep the solution if it wins
				if( math.CompareAnyLessThan( error, besterror ) )
				{
					beststart = a;
					bestend = b;
					besti = i;
					bestj = j;
					besterror = error;
					bestiteration = iterationIndex;
				}

				// advance
				if( j == count )
					break;
				part1 += m_points_weights[j];
				++j;
			}

			// advance
			part0 += m_points_weights[i];
		}
		
		// stop if we didn't improve in this iteration
		if( bestiteration != iterationIndex )
			break;
			
		// advance if possible
		++iterationIndex;
		if( iterationIndex == m_iterationCount )
			break;
			
		// stop if a new iteration is an ordering that has already been tried
		Vector3 axis = ( bestend - beststart );
		if( !ructOrdering( axis, iterationIndex ) )
			break;
	}
		
	// save the block if necessary
	if( math.CompareAnyLessThan( besterror, m_besterror ) )
	{
		// remap the indices
        fixed(byte* p_order = m_order)
        {
		byte * order = p_order + 16*bestiteration;

		byte[] unordered = new byte[16];
		for( int m = 0; m < besti; ++m )
			unordered[order[m]] = 0;
		for( int m = besti; m < bestj; ++m )
			unordered[order[m]] = 2;
		for( int m = bestj; m < count; ++m )
			unordered[order[m]] = 1;
        
		m_colours.RemapIndices( unordered, bestindices );
		
		// save the block
		colorblock.WriteColourBlock3( beststart, bestend, bestindices, block );
        }
		// save the error
		m_besterror = besterror;
	}
}

public unsafe override void Compress4( byte* block )
{
	// declare variables
	int  count = m_colours.GetCount();
	Vector4  two = Vector4.one*( 2.0f );
	Vector4  onethird_onethird2 = new Vector4( 1.0f/3.0f, 1.0f/3.0f, 1.0f/3.0f, 1.0f/9.0f );
	Vector4  twothirds_twothirds2 = new Vector4( 2.0f/3.0f, 2.0f/3.0f, 2.0f/3.0f, 4.0f/9.0f );
	Vector4  twonineths = Vector4.one*( 2.0f/9.0f );
	Vector4  half = Vector4.one*( 0.5f );

	// prepare an ordering using the principle axis
	ructOrdering( m_principle, 0 );
	
	// check all possible clusters and iterate on the total order
	Vector4 beststart = Vector4.zero;
	Vector4 bestend = Vector4.zero;
	Vector4 besterror = m_besterror;
	byte[] bestindices = new byte[16];
	int bestiteration = 0;
	int besti = 0, bestj = 0, bestk = 0;
	
	// loop over iterations (we avoid the case that all points in first or last cluster)
	for( int iterationIndex = 0;; )
	{
		// first cluster [0,i) is at the start
		Vector4 part0 = Vector4.zero;
		for( int i = 0; i < count; ++i )
		{
			// second cluster [i,j) is one third along
			Vector4 part1 = Vector4.zero;
			for( int j = i;; )
			{
				// third cluster [j,k) is two thirds along
				Vector4 part2 = ( j == 0 ) ? m_points_weights[0] : Vector4.zero;
				int kmin = ( j == 0 ) ? 1 : j;
				for( int k = kmin;; )
				{
					// last cluster [k,count) is at the end
					Vector4 part3 = m_xsum_wsum - part2 - part1 - part0;

					// compute least squares terms directly
					Vector4  alphax_sum = part2.MultiplyAdd( onethird_onethird2, part1.MultiplyAdd( twothirds_twothirds2, part0 ) );
					Vector4  alpha2_sum = alphax_sum.SplatW();
					
					Vector4  betax_sum = part1.MultiplyAdd( onethird_onethird2, part2.MultiplyAdd( twothirds_twothirds2, part3 ) );
					Vector4  beta2_sum = betax_sum.SplatW();
					
					Vector4  alphabeta_sum = Vector4.Scale(twonineths,( part1 + part2 ).SplatW());

					// compute the least-squares optimal points
					Vector4 factor = (alphabeta_sum.NegativeMultiplySubtract( alphabeta_sum, Vector4.Scale(alpha2_sum,beta2_sum)) ).Reciprocal();
					Vector4 a = Vector4.Scale(betax_sum.NegativeMultiplySubtract( alphabeta_sum, Vector4.Scale(alphax_sum,beta2_sum )),factor);
					Vector4 b = Vector4.Scale(alphax_sum.NegativeMultiplySubtract( alphabeta_sum, Vector4.Scale(betax_sum,alpha2_sum )),factor);

					// clamp to the grid
                    a = Vector4.Min(Vector4.one, Vector4.Max(Vector4.zero, a));
                    b = Vector4.Min(Vector4.one, Vector4.Max(Vector4.zero, b));
					a = Vector4.Scale(( grid.MultiplyAdd( a, half ) ).Truncate(),gridrcp);
					b = Vector4.Scale(( grid.MultiplyAdd( b, half ) ).Truncate(),gridrcp);
					
					// compute the error (we skip the ant xxsum)
					Vector4 e1 = Vector4.Scale(a,a).MultiplyAdd( alpha2_sum, Vector4.Scale(Vector4.Scale(b,b),beta2_sum) );
					Vector4 e2 = a.NegativeMultiplySubtract( alphax_sum, Vector4.Scale(Vector4.Scale(a,b),alphabeta_sum) );
					Vector4 e3 = b.NegativeMultiplySubtract( betax_sum, e2 );
					Vector4 e4 = two.MultiplyAdd( e3, e1 );

					// apply the metric to the error term
					Vector4 e5 = Vector4.Scale(e4,m_metric);
					Vector4 error = e5.SplatX() + e5.SplatY() + e5.SplatZ();

					// keep the solution if it wins
					if( math.CompareAnyLessThan( error, besterror ) )
					{
						beststart = a;
						bestend = b;
						besterror = error;
						besti = i;
						bestj = j;
						bestk = k;
						bestiteration = iterationIndex;
					}

					// advance
					if( k == count )
						break;
					part2 += m_points_weights[k];
					++k;
				}

				// advance
				if( j == count )
					break;
				part1 += m_points_weights[j];
				++j;
			}

			// advance
			part0 += m_points_weights[i];
		}
		
		// stop if we didn't improve in this iteration
		if( bestiteration != iterationIndex )
			break;
			
		// advance if possible
		++iterationIndex;
		if( iterationIndex == m_iterationCount )
			break;
			
		// stop if a new iteration is an ordering that has already been tried
		Vector3 axis = ( bestend - beststart );
		if( !ructOrdering( axis, iterationIndex ) )
			break;
	}

	// save the block if necessary
	if( math.CompareAnyLessThan( besterror, m_besterror ) )
	{
		// remap the indices
        fixed (byte* p_order = m_order)
        {
            byte* order = p_order + 16 * bestiteration;

            byte[] unordered = new byte[16];
            for (int m = 0; m < besti; ++m)
                unordered[order[m]] = 0;
            for (int m = besti; m < bestj; ++m)
                unordered[order[m]] = 2;
            for (int m = bestj; m < bestk; ++m)
                unordered[order[m]] = 3;
            for (int m = bestk; m < count; ++m)
                unordered[order[m]] = 1;

            m_colours.RemapIndices(unordered, bestindices);

            // save the block
            colorblock.WriteColourBlock4(beststart, bestend, bestindices, block);
        }
		// save the error
		m_besterror = besterror;
	}
}


        
    }

} // namespace squish

