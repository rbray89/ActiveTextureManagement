using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LibSquishPort
{
    class ColourFit
    {
        public ColourSet m_colours;
        public SquishFlags m_flags;

        public virtual unsafe void Compress3( byte* block ){}
        public virtual unsafe void Compress4( byte* block ){}

        public ColourFit(ColourSet colours, SquishFlags flags) 
        {
            m_colours = colours;
            m_flags = flags;
        }

public unsafe void Compress( byte* block )
{
    bool isDxt1 = ((m_flags & SquishFlags.kDxt1) != 0);
	if( isDxt1 )
	{
		Compress3( block );
		if( !m_colours.IsTransparent() )
			Compress4( block );
	}
	else
		Compress4( block );
}
    }
}
