/* -----------------------------------------------------------------------------

	Copyright (c) 2006 Simon Brown                          si@sjbrown.co.uk

	Permission is hereby granted, free of charge, to any person obtaining
	a copy of this software and associated documentation files (the 
	"Software"), to	deal in the Software without restriction, including
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
using System.Threading.Tasks;
using UnityEngine;
   
namespace LibSquishPort {

    public static class colorblock
    {
        private static void swap<T>(ref T a, ref T b)
{
 	T atmp = a;
    a = b;
    b = atmp;
}

static int FloatToInt( float a, int limit )
{
	// use ANSI round-to-zero behaviour to get round-to-nearest
	int i = ( int )( a + 0.5f );

	// clamp to the limit
	if( i < 0 )
		i = 0;
	else if( i > limit )
		i = limit; 

	// done
	return i;
}

static int FloatTo565( Vector3 colour )
{
	// get the components in the correct range
	int r = FloatToInt( 31.0f*colour.x, 31 );
	int g = FloatToInt( 63.0f*colour.y, 63 );
	int b = FloatToInt( 31.0f*colour.z, 31 );
	
	// pack into a single value
	return ( r << 11 ) | ( g << 5 ) | b;
}

static unsafe void WriteColourBlock( int a, int b, byte[] indices, byte* block )
{
	// get the block as bytes
	byte* bytes = block;

	// write the endpoints
	bytes[0] = ( byte )( a & 0xff );
	bytes[1] = ( byte )( a >> 8 );
	bytes[2] = ( byte )( b & 0xff );
	bytes[3] = ( byte )( b >> 8 );
	
	// write the indices
	for( int i = 0; i < 4; ++i )
	{
        fixed (byte* pindices= indices)
        {
		    byte * ind = pindices + 4*i;
		    bytes[4 + i] = (byte)(ind[0] | ( ind[1] << 2 ) | ( ind[2] << 4 ) | ( ind[3] << 6 ));
        }
	}
}

public static unsafe void WriteColourBlock3( Vector3 start, Vector3 end, byte[] indices, byte* block )
{
	// get the packed values
	int a = FloatTo565( start );
	int b = FloatTo565( end );

	// remap the indices
	byte[] remapped = new byte[16];
	if( a <= b )
	{
		// use the indices directly
		for( int i = 0; i < 16; ++i )
			remapped[i] = indices[i];
	}
	else
	{
		// swap a and b
		swap( ref a, ref b );
		for( int i = 0; i < 16; ++i )
		{
			if( indices[i] == 0 )
				remapped[i] = 1;
			else if( indices[i] == 1 )
				remapped[i] = 0;
			else
				remapped[i] = indices[i];
		}
	}
	
	// write the block
	WriteColourBlock( a, b, remapped, block );
}

public static unsafe void WriteColourBlock4( Vector3 start, Vector3 end, byte[] indices, byte* block )
{
	// get the packed values
	int a = FloatTo565( start );
	int b = FloatTo565( end );

	// remap the indices
	byte[] remapped = new byte[16];
	if( a < b )
	{
		// swap a and b
		swap( ref a, ref b );
		for( int i = 0; i < 16; ++i )
			remapped[i] = (byte)(( indices[i] ^ 0x1 ) & 0x3);
	}
	else if( a == b )
	{
		// use index 0
		for( int i = 0; i < 16; ++i )
			remapped[i] = 0;
	}
	else
	{
		// use the indices directly
		for( int i = 0; i < 16; ++i )
			remapped[i] = indices[i];
	}
	
	// write the block
	WriteColourBlock( a, b, remapped, block );
}

static unsafe int Unpack565( byte* packed, byte* colour )
{
	// build the packed value
	int value = ( int )packed[0] | ( ( int )packed[1] << 8 );
	
	// get the components in the stored range
	byte red = ( byte )( ( value >> 11 ) & 0x1f );
	byte green = ( byte )( ( value >> 5 ) & 0x3f );
	byte blue = ( byte )( value & 0x1f );

	// scale up to 8 bits
	colour[0] = (byte)(( red << 3 ) | ( red >> 2 ));
	colour[1] = (byte)(( green << 2 ) | ( green >> 4 ));
	colour[2] = (byte)(( blue << 3 ) | ( blue >> 2 ));
	colour[3] = 255;
	
	// return the value
	return value;
}

public static unsafe void DecompressColour( byte[] rgba, byte[] block, bool isDxt1 )
{
    // unpack the endpoints
	byte[] codes = new byte[16];

    int a, b;
	// get the block bytes	
    fixed (byte* bytes = block, pcodes = codes )
        {
	
	a = Unpack565( bytes, pcodes );
	 b = Unpack565( bytes + 2, pcodes + 4 );
	}
	// generate the midpoints
	for( int i = 0; i < 3; ++i )
	{
		int c = codes[i];
		int d = codes[4 + i];

		if( isDxt1 && a <= b )
		{
			codes[8 + i] = ( byte )( ( c + d )/2 );
			codes[12 + i] = 0;
		}
		else
		{
			codes[8 + i] = ( byte )( ( 2*c + d )/3 );
			codes[12 + i] = ( byte )( ( c + 2*d )/3 );
		}
	}
	
	// fill in alpha for the intermediate values
	codes[8 + 3] = 255;
	codes[12 + 3] = (byte)(( isDxt1 && a <= b ) ? 0 : 255);
	
	// unpack the indices
	byte[] indices = new byte[16];
	for( int i = 0; i < 4; ++i )
	{
        fixed (byte* pindices = indices)
        {
            byte* ind = pindices + 4 * i;
            byte packed = block[4 + i];

            ind[0] = (byte)(packed & 0x3);
            ind[1] = (byte)((packed >> 2) & 0x3);
            ind[2] = (byte)((packed >> 4) & 0x3);
            ind[3] = (byte)((packed >> 6) & 0x3);
        }
	}

	// store out the colours
	for( int i = 0; i < 16; ++i )
	{
		byte offset = (byte)(4*indices[i]);
		for( int j = 0; j < 4; ++j )
			rgba[4*i + j] = codes[offset + j];
	}
}
}
} // namespace squish

