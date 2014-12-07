/* -----------------------------------------------------------------------------

        Copyright (c) 2006 Simon Brown                          si@sjbrown.co.uk

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
    class RangeFit : ColourFit
    {
        Vector3 m_metric;
	    Vector3 m_start;
	    Vector3 m_end;
	    float m_besterror;
        static Vector3 grid = new Vector3(31.0f, 63.0f, 31.0f);
        static Vector3 gridrcp = new Vector3(1.0f / 31.0f, 1.0f / 63.0f, 1.0f / 31.0f);

      public  RangeFit( ColourSet  colours, SquishFlags flags ) 
  : base( colours, flags )
{
	// initialise the metric
	bool perceptual = ( ( m_flags & SquishFlags.kColourMetricPerceptual ) != 0 );
	if( perceptual )
		m_metric = new Vector3( 0.2126f, 0.7152f, 0.0722f );
	else
		m_metric = Vector3.one;

	// initialise the best error
	m_besterror = float.MaxValue;

	// cache some values
	int  count = m_colours.GetCount();
	Vector3[]  values = m_colours.GetPoints();
	float[]  weights = m_colours.GetWeights();
	
	// get the covariance matrix
	Sym3x3 covariance = math.ComputeWeightedCovariance( count, values, weights );
	
	// compute the principle component
	Vector3 principle = math.ComputePrincipleComponent( covariance );

	// get the min and max range as the codebook endpoints
	Vector3 start = Vector3.zero;
	Vector3 end = Vector3.zero;
	if( count > 0 )
	{
		float min, max;
		
		// compute the range
		start = end = values[0];
		min = max = Vector3.Dot( values[0], principle );
		for( int i = 1; i < count; ++i )
		{
			float val = Vector3.Dot( values[i], principle );
			if( val < min )
			{
				start = values[i];
				min = val;
			}
			else if( val > max )
			{
				end = values[i];
				max = val;
			}
		}
	}
			
	// clamp the output to [0, 1]
	start = Vector3.Min( Vector3.one,Vector3.Max( Vector3.zero, start ) );
	end = Vector3.Min( Vector3.one, Vector3.Max( Vector3.zero, end ) );

	// clamp to the grid and save
	
	Vector3  half = Vector3.one*( 0.5f );
	m_start = Vector3.Scale(math.Truncate( Vector3.Scale(grid,start) + half ),gridrcp);
	m_end = Vector3.Scale(math.Truncate( Vector3.Scale(grid,end) + half ),gridrcp);
}

public override unsafe void Compress3( byte* block )
{
	// cache some values
	int  count = m_colours.GetCount();
	Vector3[]  values = m_colours.GetPoints();
	
	// create a codebook
	Vector3[] codes = new Vector3[3];
	codes[0] = m_start;
	codes[1] = m_end;
	codes[2] = 0.5f*m_start + 0.5f*m_end;

	// match each point to the closest code
	byte[] closest = new byte[16];
	float error = 0.0f;
	for( int i = 0; i < count; ++i )
	{
		// find the closest code
		float dist = float.MaxValue;
		int idx = 0;
		for( int j = 0; j < 3; ++j )
		{
			float d = Vector3.Scale( m_metric,( values[i] - codes[j] ) ).sqrMagnitude;
			if( d < dist )
			{
				dist = d;
				idx = j;
			}
		}
		
		// save the index
		closest[i] = ( byte )idx;
		
		// accumulate the error
		error += dist;
	}
	
	// save this scheme if it wins
	if( error < m_besterror )
	{
		// remap the indices
		byte[] indices = new byte[16];
		m_colours.RemapIndices( closest, indices );
		
		// save the block
		colorblock.WriteColourBlock3( m_start, m_end, indices, block );
		
		// save the error
		m_besterror = error;
	}
}

public override unsafe void Compress4( byte* block )
{
	// cache some values
	int  count = m_colours.GetCount();
	Vector3[]  values = m_colours.GetPoints();
	
	// create a codebook
	Vector3[] codes = new Vector3[4];
	codes[0] = m_start;
	codes[1] = m_end;
	codes[2] = ( 2.0f/3.0f )*m_start + ( 1.0f/3.0f )*m_end;
	codes[3] = ( 1.0f/3.0f )*m_start + ( 2.0f/3.0f )*m_end;

	// match each point to the closest code
	byte[] closest = new byte[16];
	float error = 0.0f;
	for( int i = 0; i < count; ++i )
	{
		// find the closest code
		float dist = float.MaxValue;
		int idx = 0;
		for( int j = 0; j < 4; ++j )
		{
			float d = Vector3.Scale( m_metric,( values[i] - codes[j] ) ).sqrMagnitude;
			if( d < dist )
			{
				dist = d;
				idx = j;
			}
		}
		
		// save the index
		closest[i] = ( byte )idx;
		
		// accumulate the error
		error += dist;
	}
	
	// save this scheme if it wins
	if( error < m_besterror )
	{
		// remap the indices
		byte[] indices = new byte[16];
		m_colours.RemapIndices( closest, indices );
		
		// save the block
        colorblock.WriteColourBlock4(m_start, m_end, indices, block);

		// save the error
		m_besterror = error;
	}
}


    }
}
