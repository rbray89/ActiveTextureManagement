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

namespace LibSquishPort
{
    [Flags]
    public enum SquishFlags
    {
        //! Use DXT1 compression.
        kDxt1 = (1 << 0),

        //! Use DXT3 compression.
        kDxt3 = (1 << 1),

        //! Use DXT5 compression.
        kDxt5 = (1 << 2),

        //! Use a very slow but very high quality colour compressor.
        kColourIterativeClusterFit = (1 << 8),

        //! Use a slow but high quality colour compressor (the default).
        kColourClusterFit = (1 << 3),

        //! Use a fast but low quality colour compressor.
        kColourRangeFit = (1 << 4),

        //! Use a perceptual metric for colour error (the default).
        kColourMetricPerceptual = (1 << 5),

        //! Use a uniform metric for colour error.
        kColourMetricUniform = (1 << 6),

        //! Weight the colour by alpha during cluster fit (disabled by default).
        kWeightColourByAlpha = (1 << 7)
    };

    public class squish
    {
        static SquishFlags FixFlags(SquishFlags flags)
        {
            // grab the flag bits
            SquishFlags method = flags & (SquishFlags.kDxt1 | SquishFlags.kDxt5);
            SquishFlags fit = flags & (SquishFlags.kColourIterativeClusterFit | SquishFlags.kColourClusterFit | SquishFlags.kColourRangeFit);
            SquishFlags metric = flags & (SquishFlags.kColourMetricPerceptual | SquishFlags.kColourMetricUniform);
            SquishFlags extra = flags & SquishFlags.kWeightColourByAlpha;

            // set defaults
            if (method != SquishFlags.kDxt3 && method != SquishFlags.kDxt5)
                method = SquishFlags.kDxt1;
            if (fit != SquishFlags.kColourRangeFit)
                fit = SquishFlags.kColourClusterFit;
            if (metric != SquishFlags.kColourMetricUniform)
                metric = SquishFlags.kColourMetricPerceptual;

            // done
            return method | fit | metric | extra;
        }

        unsafe void Compress(byte[] rgba, byte* block, SquishFlags flags)
        {
            // compress with full mask
            CompressMasked(rgba, 0xffff, block, flags);
        }

        static unsafe void CompressMasked(byte[] rgba, int mask, byte* pBlock, SquishFlags flags)
        {
            // fix any bad flags
            flags = FixFlags(flags);


            // get the block locations
            byte* colourBlock = pBlock;
            byte* alphaBock = pBlock;
            if ((flags & (SquishFlags.kDxt3 | SquishFlags.kDxt5)) != 0)
                colourBlock = pBlock + 8;

            // create the minimal point set
            ColourSet colours = new ColourSet(rgba, mask, flags);

            // check the compression type and compress colour
            if (colours.GetCount() == 1)
            {
                // always do a single colour fit
                SingleColourFit fit = new SingleColourFit(colours, flags);
                fit.Compress(colourBlock);
            }
            else if ((flags & SquishFlags.kColourRangeFit) != 0 || colours.GetCount() == 0)
            {
                // do a range fit
                RangeFit fit = new RangeFit(colours, flags);
                fit.Compress(colourBlock);
            }
            else
            {
                // default to a cluster fit (could be iterative or not)
                ClusterFit fit = new ClusterFit(colours, flags);
                fit.Compress(colourBlock);
            }

            // compress alpha separately if necessary
            if ((flags & SquishFlags.kDxt3) != 0)
            {
                alpha.CompressAlphaDxt3(rgba, mask, alphaBock);
            }
            else if ((flags & SquishFlags.kDxt5) != 0)
            {
                alpha.CompressAlphaDxt5(rgba, mask, alphaBock);
            }
        }

        /*
        void Decompress( u8* rgba, void const* block, int flags )
        {
            // fix any bad flags
            flags = FixFlags( flags );

            // get the block locations
            void const* colourBlock = block;
            void const* alphaBock = block;
            if( ( flags & ( kDxt3 | kDxt5 ) ) != 0 )
                colourBlock = reinterpret_cast< u8 const* >( block ) + 8;

            // decompress colour
            DecompressColour( rgba, colourBlock, ( flags & kDxt1 ) != 0 );

            // decompress alpha separately if necessary
            if( ( flags & kDxt3 ) != 0 )
                DecompressAlphaDxt3( rgba, alphaBock );
            else if( ( flags & kDxt5 ) != 0 )
                DecompressAlphaDxt5( rgba, alphaBock );
        }
                */
        int GetStorageRequirements(int width, int height, SquishFlags flags)
        {
            // fix any bad flags
            flags = FixFlags(flags);

            // compute the storage requirements
            int blockcount = ((width + 3) / 4) * ((height + 3) / 4);
            int blocksize = ((flags & SquishFlags.kDxt1) != 0) ? 8 : 16;
            return blockcount * blocksize;
        }

        public static unsafe void CompressImage(byte[] rgba, int width, int height, byte[] blocks, SquishFlags flags)
        {
            // fix any bad flags
            flags = FixFlags(flags);

            // initialise the block output
            fixed (byte* pblocks = blocks, prgba = rgba)
            {
                byte* targetBlock = (pblocks);
                int bytesPerBlock = ((flags & SquishFlags.kDxt1) != 0) ? 8 : 16;

                // loop over blocks
                for (int y = 0; y < height; y += 4)
                {
                    for (int x = 0; x < width; x += 4)
                    {
                        // build the 4x4 block of pixels
                        byte[] sourceRgba = new byte[16 * 4];
                        fixed (byte* psourceRgba = sourceRgba)
                        {
                            byte* targetPixel = psourceRgba;
                            int mask = 0;
                            for (int py = 0; py < 4; ++py)
                            {
                                for (int px = 0; px < 4; ++px)
                                {
                                    // get the source pixel in the image
                                    int sx = x + px;
                                    int sy = y + py;

                                    // enable if we're in the image
                                    if (sx < width && sy < height)
                                    {
                                        // copy the rgba value
                                        byte* sourcePixel = prgba + 4 * (width * sy + sx);
                                        for (int i = 0; i < 4; ++i)
                                            *targetPixel++ = *sourcePixel++;

                                        // enable this pixel
                                        mask |= (1 << (4 * py + px));
                                    }
                                    else
                                    {
                                        // skip this pixel as its outside the image
                                        targetPixel += 4;
                                    }
                                }
                            }

                            // compress it into the output
                            CompressMasked(sourceRgba, mask, targetBlock, flags);
                        }
                        // advance
                        targetBlock += bytesPerBlock;
                    }
                }
            }
        }

        /*
void DecompressImage( u8* rgba, int width, int height, void const* blocks, int flags )
{
    // fix any bad flags
    flags = FixFlags( flags );

    // initialise the block input
    u8 const* sourceBlock = reinterpret_cast< u8 const* >( blocks );
    int bytesPerBlock = ( ( flags & kDxt1 ) != 0 ) ? 8 : 16;

    // loop over blocks
    for( int y = 0; y < height; y += 4 )
    {
        for( int x = 0; x < width; x += 4 )
        {
            // decompress the block
            u8 targetRgba[4*16];
            Decompress( targetRgba, sourceBlock, flags );
			
            // write the decompressed pixels to the correct image locations
            u8 const* sourcePixel = targetRgba;
            for( int py = 0; py < 4; ++py )
            {
                for( int px = 0; px < 4; ++px )
                {
                    // get the target location
                    int sx = x + px;
                    int sy = y + py;
                    if( sx < width && sy < height )
                    {
                        u8* targetPixel = rgba + 4*( width*sy + sx );
						
                        // copy the rgba value
                        for( int i = 0; i < 4; ++i )
                            *targetPixel++ = *sourcePixel++;
                    }
                    else
                    {
                        // skip this pixel as its outside the image
                        sourcePixel += 4;
                    }
                }
            }
			
            // advance
            sourceBlock += bytesPerBlock;
        }
    }
}*/
    }
}
