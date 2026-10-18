
#ifndef PREVIEW_INCLUDED
#define PREVIEW_INCLUDED

#include "UnityCG.cginc"

// @diogo: node previews are slices of Texture2DArray render textures ( ASEPreviewSlices.cs ). A port input is
// the array plus the slice index the editor binds next to it ( <name>_Slice ), sampled with the array's own
// sampler state; needs #pragma target 3.5.
#define ASE_PORT( name ) Texture2DArray name; SamplerState sampler##name; float name##_Slice
#define ASE_SAMPLE_PORT( name, uv ) name.Sample( sampler##name, float3( uv, name##_Slice ) )

float3 preview_WorldSpaceCameraPos;

float4x4 preview_WorldToObject;
float4x4 preview_ObjectToWorld;
float4x4 preview_MatrixV;
float4x4 preview_MatrixInvV;
float4x4 preview_MatrixP;

float3 preview_EditorLightDirection;
float3 preview_EditorLightColor;
float preview_EditorLightIntensity;

float preview_EditorTime;
float preview_EditorDeltaTime;
float preview_EditorSmoothDeltaTime;
float preview_EditorLastTime;

inline float3 PreviewFragmentPositionOS( float2 uv )
{
	float2 xy = 2 * uv - 1;
	float z = -sqrt( 1 - saturate( dot( xy, xy ) ) );
	return float3( xy, z );
}

inline float3 PreviewFragmentNormalOS( float2 uv, bool normalized = true )
{
	float3 positionOS = PreviewFragmentPositionOS( uv );
	float3 normalOS = positionOS;
	if ( normalized )
	{
		normalOS = normalize( normalOS );
	}
	return normalOS;
}

inline float3 PreviewFragmentTangentOS( float2 uv, bool normalized = true )
{
	float3 positionOS = PreviewFragmentPositionOS( uv );
	float3 tangentOS = float3( -positionOS.z, positionOS.y * 0.01, positionOS.x );
	if ( normalized )
	{
		tangentOS = normalize( tangentOS );
	}
	return tangentOS;
}

inline float3 PreviewWorldSpaceViewDir( in float3 worldPos, bool normalized )
{
	float3 vec = preview_WorldSpaceCameraPos.xyz - worldPos;
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

inline float3 PreviewWorldToObjectDir( in float3 dir, const bool normalized )
{
	float3 vec = mul( ( float3x3 )preview_WorldToObject, dir );
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

inline float3 PreviewObjectToWorldDir( in float3 dir, const bool normalized )
{
	float3 vec = mul( ( float3x3 )preview_ObjectToWorld, dir );
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

inline float3 PreviewWorldToViewDir( in float3 dir, const bool normalized )
{
	float3 vec = mul( ( float3x3 )preview_MatrixV, dir );
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

inline float3 PreviewViewToWorldDir( in float3 dir, const bool normalized )
{
	float3 vec = mul( ( float3x3 )preview_MatrixInvV, dir );
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

// @diogo: rows are the world-space tangent, binormal and normal, matching the generated ase_worldToTangent
float3x3 PreviewFragmentWorldToTangentMatrix( in float2 uv )
{
	float3 tangent = PreviewFragmentTangentOS( uv );
	float3 normal = PreviewFragmentNormalOS( uv );
	float3 worldNormal = UnityObjectToWorldNormal( normal );
	float3 worldTangent = UnityObjectToWorldDir( tangent );

	const float tangentSign = -1;
	float3 worldBinormal = normalize( cross( worldNormal, worldTangent ) * tangentSign );

	return float3x3( worldTangent, worldBinormal, worldNormal );
}

float3 PreviewFragmentWorldToTangentDir( in float2 uv, in float3 dirWS, const bool normalized )
{
	float3 vec = mul( PreviewFragmentWorldToTangentMatrix( uv ), dirWS );
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

float3 PreviewFragmentTangentToWorldDir( in float2 uv, in float3 normalTS, const bool normalized )
{
	float3 vec = mul( normalTS, PreviewFragmentWorldToTangentMatrix( uv ) );
	if ( normalized )
	{
		vec = normalize( vec );
	}
	return vec;
}

float2 PreviewFragmentSphericalUV( in float2 uv )
{
	float3 vertexPos = PreviewFragmentPositionOS( uv );
	return float2( atan2( vertexPos.x, -vertexPos.z ) / UNITY_PI + 0.5, uv.y );
}

#endif // PREVIEW_INCLUDED