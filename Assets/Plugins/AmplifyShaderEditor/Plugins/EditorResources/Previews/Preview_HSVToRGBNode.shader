Shader "Hidden/HSVToRGBNode"
{
	Properties
	{
		_A ( "_Hue",  2DArray ) = "white" {}
		_B ( "_Saturation",  2DArray ) = "white" {}
		_C ( "_Value",  2DArray ) = "white" {}
	}

	SubShader
	{
		Pass
		{
			CGPROGRAM
			#include "UnityCG.cginc"
			#include "Preview.cginc"
			#pragma vertex vert_img
			#pragma fragment frag
			#pragma target 3.5

			ASE_PORT( _A );
			ASE_PORT( _B );
			ASE_PORT( _C );

			float3 HSVToRGB( float3 c )
			{
				float4 K = float4( 1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0 );
				float3 p = abs( frac( c.xxx + K.xyz ) * 6.0 - K.www );
				return c.z * lerp( K.xxx, saturate( p - K.xxx ), c.y );
			}

			float4 frag ( v2f_img i ) : SV_Target
			{
				float h = ASE_SAMPLE_PORT( _A, i.uv ).r;
				float s = ASE_SAMPLE_PORT( _B, i.uv ).r;
				float v = ASE_SAMPLE_PORT( _C, i.uv ).r;

				return float4( HSVToRGB(float3(h,s,v)), 1 );
			}
			ENDCG
		}
	}
}
