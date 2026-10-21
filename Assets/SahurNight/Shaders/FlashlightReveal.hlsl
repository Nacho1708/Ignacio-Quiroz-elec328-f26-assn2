// Enchanted Flashlight math, used by the EnchantedFlashlight Shader Graph (Custom Function nodes).
//
// The flashlight script (EnchantedFlashlight.cs) sends these to every material every frame:
//   Origin    - world position of the flashlight bulb          (_Flashlight_Origin)
//   Direction - which way the flashlight points               (_Flashlight_Direction)
//   ConeAngle - full width of the beam, in degrees            (_Flashlight_ConeAngle)
//   On        - 0 = off, 1 = on (in between while it flickers) (_Flashlight_On)
//   Range     - how far the beam reaches, in meters           (_Flashlight_Range)

#ifndef ENCHANTED_FLASHLIGHT_INCLUDED
#define ENCHANTED_FLASHLIGHT_INCLUDED

// How far (in degrees) this point is from the middle of the beam, and whether the beam can reach it.
float FlashlightAngle(float3 positionWS, float3 normalWS, float3 origin, float3 direction, float range,
                      out float reachable)
{
    float3 toSurface = positionWS - origin;
    float dist = length(toSurface);
    float3 toSurfaceDir = toSurface / max(dist, 1e-4);

    // The dot product of two unit vectors is the cosine of the angle between them:
    // 1 straight down the beam, 0 at 90 degrees off to the side.
    float cosAngle = dot(toSurfaceDir, normalize(direction));

    // Surfaces facing away from the flashlight (like the far side of a cube) stay normal:
    // their normal points away from the light, so dot(normal, toward-the-light) is negative.
    float facing = smoothstep(0.0, 0.08, dot(normalize(normalWS), -toSurfaceDir));
    float inRange = 1.0 - smoothstep(range * 0.85, range, dist);
    reachable = facing * inRange;

    return degrees(acos(clamp(cosAngle, -1.0, 1.0)));
}

// Fragment stage: how much of the hidden texture shows here (Reveal), where the outline ring is
// (Boundary), and a wobbly, slowly moving UV for the hidden texture so the magic looks alive.
void FlashlightReveal_float(float3 PositionWS, float3 NormalWS, float2 UV,
                            float3 Origin, float3 Direction, float ConeAngle, float On, float Range,
                            float BoundaryWidth, float EdgeFeather,
                            out float Reveal, out float Boundary, out float2 HiddenUV)
{
    float reachable;
    float angle = FlashlightAngle(PositionWS, NormalWS, Origin, Direction, Range, reachable);

    float halfCone = ConeAngle * 0.5;
    float innerEdge = halfCone - BoundaryWidth; // where the hidden texture stops and the outline starts
    float feather = max(EdgeFeather, 1e-3);

    float inside = 1.0 - smoothstep(innerEdge - feather, innerEdge, angle);
    float ring = smoothstep(innerEdge - feather, innerEdge, angle) * (1.0 - smoothstep(halfCone, halfCone + feather, angle));

    Reveal = inside * reachable * On;
    Boundary = ring * reachable * On;

    float t = _TimeParameters.x;
    HiddenUV = UV + float2(0.008 * sin(t * 2.1 + UV.y * 25.0), 0.008 * cos(t * 1.7 + UV.x * 25.0))
                  + float2(0.01, 0.0) * sin(t * 0.3);
}

// Vertex stage: inside the beam, vertices wiggle in and out along their normals.
void FlashlightWiggle_float(float3 PositionWS, float3 NormalWS,
                            float3 Origin, float3 Direction, float ConeAngle, float On, float Range,
                            float Amount, out float3 PositionOS)
{
    float reachable;
    float angle = FlashlightAngle(PositionWS, NormalWS, Origin, Direction, Range, reachable);
    float inside = (1.0 - smoothstep(ConeAngle * 0.5 - 2.0, ConeAngle * 0.5, angle)) * reachable * On;

    float wave = sin(_TimeParameters.x * 9.0 + dot(PositionWS, float3(13.0, 17.0, 11.0)));
    float3 moved = PositionWS + normalize(NormalWS) * (Amount * wave * inside);
    PositionOS = TransformWorldToObject(moved);
}

#endif
