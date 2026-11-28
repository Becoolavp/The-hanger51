#ifndef RNG_BLOOD_SHAPES
#define RNG_BLOOD_SHAPES

struct Kit
{
    float4 phase;
    float2 spots[9];
    float sizes[9];
    float tail;
};

float2 Hash22(float2 p)
{
    float3 q = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    q += dot(q, q.yzx + 33.33);
    return frac((q.xx + q.yz) * q.zy);
}

float Noise2(float2 p)
{
    float2 cell = floor(p);
    float2 local = frac(p);
    float2 blend = local * local * (3.0 - 2.0 * local);
    float a = Hash22(cell).x;
    float b = Hash22(cell + float2(1.0, 0.0)).x;
    float c = Hash22(cell + float2(0.0, 1.0)).x;
    float d = Hash22(cell + float2(1.0, 1.0)).x;
    return lerp(lerp(a, b, blend.x), lerp(c, d, blend.x), blend.y);
}

float SmoothUnion(float a, float b, float k)
{
    float h = saturate(0.5 + 0.5 * (b - a) / k);
    return lerp(b, a, h) - k * h * (1.0 - h);
}

float Segment(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / dot(ba, ba));
    return length(pa - ba * h);
}

float4 Phase(float seed, float drift)
{
    return frac(sin(seed * float4(12.9898, 78.233, 37.719, 93.989)) * 43758.5453) * 6.2831853 + drift;
}

float Wobble(float angle, float4 phase)
{
    return 0.04 * sin(angle * 2.0 + phase.x) + 0.055 * sin(angle * 3.0 + phase.y) + 0.045 * sin(angle * 5.0 + phase.z) + 0.03 * sin(angle * 9.0 + phase.w) + 0.018 * sin(angle * 14.0 + phase.x * 1.7 + phase.w);
}

void Specks(float seed, float stretch, out float2 spots[9], out float sizes[9], out float tail)
{
    float lean = saturate((stretch - 1.0) * 0.5);
    tail = (Hash22(float2(seed, 7.0)).x - 0.5) * 0.2;
    float spread = lerp(6.2831853, 1.5, lean);
    for (int i = 0; i < 9; i++)
    {
        float2 a = Hash22(float2(seed * 1.37 + 0.5, i * 7.0 + 1.0));
        float2 b = Hash22(float2(i * 3.0 + 2.0, seed * 2.11 + 3.7));
        float turn = (a.x - 0.5) * spread;
        float reach = 0.5 + 0.42 * a.y;
        spots[i] = float2(cos(turn), sin(turn)) * reach;
        sizes[i] = 0.02 + 0.07 * b.x * b.x;
    }
}

float Splash(float2 p, float stretch, float body, Kit kit)
{
    float angle = atan2(p.y, p.x);
    float lean = saturate((stretch - 1.0) * 0.5);
    float dist = length(p) - (0.36 + Wobble(angle, kit.phase)) * body;

    float2 tailEnd = float2(0.25 + 0.5 * lean, kit.tail);
    float along = saturate(p.x / max(0.01, tailEnd.x));
    float tail = Segment(p, float2(0.1, 0.0), tailEnd) - 0.16 * (1.0 - 0.8 * along) * body * lean;
    dist = SmoothUnion(dist, tail, 0.1);

    for (int i = 0; i < 9; i++)
        dist = min(dist, length(p - kit.spots[i]) - kit.sizes[i]);

    return dist;
}

float Rays(float2 p, float seed, float count, float reachMin, float reachMax, float core, float thickness, float4 phase)
{
    p += (float2(Noise2(p * 3.0 + seed * 5.0), Noise2(p * 3.0 + seed * 9.0 + 4.7)) - 0.5) * 0.16;
    float radius = length(p);
    float angle = atan2(p.y, p.x) + (Noise2(float2(radius * 2.5, seed * 3.0)) - 0.5) * 0.5;
    float dist = radius - (core + Wobble(angle, phase) * 0.9 * saturate(core * 4.0));
    float sector = (angle / 6.2831853 + 0.5) * count;
    float id = floor(sector);
    float2 pick = Hash22(float2(id * 3.1 + 1.0, seed * 1.73 + 2.0));
    float2 more = Hash22(float2(seed * 2.9 + 4.0, id * 1.7 + 6.0));
    float shift = (more.y - 0.5) * 0.5;
    float side = abs(frac(sector) - 0.5 - shift);
    float reach = lerp(reachMin, reachMax, pick.x * pick.x) * step(0.3, more.x);
    float lateral = side * radius * 6.2831853 / count;
    float taper = saturate(1.0 - radius / max(0.001, reach));
    float spoke = max(lateral - (thickness * (0.45 + more.y) * taper * taper + 0.006), radius - reach);
    dist = SmoothUnion(dist, spoke, 0.05);
    float turn = ((id + 0.5 + shift) / count - 0.5) * 6.2831853;
    float2 tip = float2(cos(turn), sin(turn)) * min(0.93, reach + 0.05 + 0.08 * pick.y);
    return min(dist, length(p - tip) - (0.014 + 0.035 * pick.y) * saturate(reach * 4.0));
}

float Streak(float2 p, float seed, float specks)
{
    float wave = Noise2(float2(p.x * 3.0 + seed * 7.0, seed));
    float width = (0.16 + 0.3 * wave) * saturate((0.95 - abs(p.x)) * 4.0);
    float dist = abs(p.y + (wave - 0.5) * 0.25) - width;
    float gaps = 0.32 - Noise2(float2(p.x * 2.2 + seed * 3.0, seed * 5.0));
    dist = max(dist, gaps * 0.6);
    for (int i = 0; i < 5; i++)
    {
        float2 a = Hash22(float2(seed * 1.9 + 0.2, i * 4.0 + 5.0));
        float2 bead = float2(a.x * 1.8 - 0.9, (a.y - 0.5) * 0.7);
        dist = min(dist, length(p - bead) - (0.08 + 0.14 * a.y) * specks);
    }

    return dist;
}

static float foldRise = 0.0;
static float foldCrease = 0.0;

float RunPuddle(float2 p, float seed, float body, float age, float stretch)
{
    float dist = 1.0;
    float beyond = (p.x - foldCrease) * stretch;
    for (int i = 0; i < 4; i++)
    {
        float2 a = Hash22(float2(seed * 2.3 + 0.7, i * 5.0 + 3.0));
        if (i > 0 && Hash22(float2(seed * 3.1 + i, 9.0)).x < 0.55) continue;

        float lane = (a.x - 0.5) * 0.5;
        float grown = saturate(age / (1.5 + 3.1 * a.y));
        float tip = -0.5 + (0.25 + 1.1 * a.y) * (1.0 - (1.0 - grown) * (1.0 - grown));
        float thick = (0.028 + 0.022 * a.x) * body;
        float over = (tip - foldCrease) * stretch;
        if (over <= 0.0) continue;

        float radius = min(0.55, sqrt(over * thick * 2.2) + thick * 0.8);
        float2 at = float2(beyond * 2.2, (p.y - lane) * 0.85);
        float angle = atan2(at.y, at.x);
        float wobble = (Noise2(float2(angle * 2.7 + seed * 7.0, seed + i)) - 0.5) * 0.6 * radius + (Noise2(at * 31.0 + seed) - 0.5) * 0.08;
        dist = SmoothUnion(dist, length(at) - radius - wobble, 0.05);
    }

    return dist;
}

float RunWall(float2 p, float seed, float body, float specks, float age, Kit kit)
{
    float2 top = float2((p.x + 0.5) * 2.2, p.y);
    float dist = Splash(top, 1.0, body, kit);
    for (int i = 0; i < 4; i++)
    {
        float2 a = Hash22(float2(seed * 2.3 + 0.7, i * 5.0 + 3.0));
        if (i > 0 && Hash22(float2(seed * 3.1 + i, 9.0)).x < 0.55) continue;

        float lane = (a.x - 0.5) * 0.5;
        float grown = saturate(age / (1.5 + 3.1 * a.y));
        float reach = (0.25 + 1.1 * a.y) * (1.0 - (1.0 - grown) * (1.0 - grown)) * specks;
        float thick = (0.028 + 0.022 * a.x) * body;
        float drip = Segment(p, float2(-0.5, lane), float2(-0.5 + reach, lane)) - thick;
        float bead = length((p - float2(-0.5 + reach, lane)) * float2(2.2, 1.0)) - thick * 1.45;
        dist = min(dist, min(drip, bead));
    }

    return dist;
}

float Run(float2 p, float seed, float body, float specks, float age, Kit kit, float stretch)
{
    float dist = 1.0;
    [branch]
    if (foldRise > 0.0) dist = RunPuddle(p, seed, body, age, stretch);
    else dist = RunWall(p, seed, body, specks, age, kit);
    return dist;
}

#define SMEAR_PAD 1.5

static float smearPixel = 0.0;

float Band(float frequency)
{
    return saturate(1.5 - smearPixel * frequency * 2.0);
}

// how dry (above 0) or wet (below 0) a smear piece looks at p, blended from its start to its end; kept as two bytes in the fraction of
// its open flag, and a plain smear reads 0
float SmearLook(float flags, float2 p)
{
    float f = frac(flags) * 256.0;
    if (f < 0.5) return 0.0;

    float2 ends = (float2(floor(f), round(frac(f) * 256.0)) - 1.0) / 127.0 - 1.0;
    return lerp(ends.x, ends.y, saturate(p.x * 0.5 + 0.5));
}

float Smear(float2 p, float4 frame, float smear)
{
    float look = SmearLook(frame.w, p);
    float wet = saturate(-look);
    float dry = saturate(look);
    float code = (smear - 5.0) * 4.0;
    float caps = floor(code);
    float strength = saturate((code - caps) / 0.96);
    float box = 1.0 / max(0.0001, frame.z);
    float reach = box / SMEAR_PAD;
    float path = frame.x + p.x / max(0.0001, frame.y);
    float travelled = fmod(path, 100.0);
    float along = travelled + frac(floor(path / 100.0) * 0.618034) * 50.0;
    float across = p.y * box;
    float centre = (Noise2(float2(along * 2.2, 3.7)) - 0.5) * 0.3 * reach + (Noise2(float2(along * 1.4, 8.3)) - 0.5) * 0.025;
    float lead = max(reach * 1.6, 0.06);
    float width = reach * lerp(0.42 + 0.5 * strength, 0.95, wet) * (0.8 + 0.4 * Noise2(float2(along * max(3.1, 0.25 / reach), 9.1)));
    width *= sqrt(saturate(travelled / lead));
    if (floor(frame.w) > 0.5) width *= sqrt(saturate((1.0 - p.x) / max(0.0001, frame.y) / lead));
    float scale = max(1.0, 0.12 / reach);
    float edge = ((Noise2(float2(along * 9.0 * scale, across * 4.0 + 1.3)) - 0.5) * 0.3 * Band(9.0 * scale) + (Noise2(float2(along * 31.0 * scale, across * 9.0 + 7.7)) - 0.5) * 0.18 * Band(31.0 * scale)) * reach;
    float dist = (abs(across - centre) - width - edge) / reach;
    bool tail = caps > 1.5;
    if (tail)
    {
        float t = saturate((p.x + 0.2) / 1.15);
        dist = max(dist, abs(across - centre) / reach - (1.0 - t * t) * (0.7 + 0.2 * strength));
    }

    float fine = max(38.0, 4.0 / reach);
    float lane = across - centre + (Noise2(float2(along * 2.6 * scale, (across - centre) / reach * 1.7 + 21.0)) - 0.5) * 0.22 * reach;
    float lanes = lerp(0.5, Noise2(float2(lane * fine, along * 3.5 + 5.0)), Band(fine)) * 0.7 + lerp(0.5, Noise2(float2(lane * fine * 2.4, along * 6.0)), Band(fine * 2.4)) * 0.3;
    float thin = lerp(0.08 + 0.45 * (1.0 - strength) + 0.4 * (1.0 - saturate(travelled / (lead * 3.0))), -0.3, wet) + 0.3 * dry;
    dist = max(dist, (thin - lanes) * 0.6);
    float skip = Noise2(float2(along * 2.4 * scale, 17.3)) + (Noise2(float2(along * 8.0 * scale, across / reach * 1.6 + 3.3)) - 0.5) * 0.3 - (0.78 + 0.5 * strength) - wet + 0.35 * dry;
    dist = max(dist, skip * 1.2);
    return dist;
}

void Lobes(float seed, float grown, out float2 spots[9], out float sizes[9])
{
    for (int i = 0; i < 9; i++)
    {
        spots[i] = 0.0;
        sizes[i] = 0.0;
    }

    for (int j = 0; j < 3; j++)
    {
        float2 a = Hash22(float2(seed * 1.91 + 0.3, j * 5.0 + 2.0));
        float turn = a.x * 6.2831853;
        spots[j] = float2(cos(turn), sin(turn)) * (0.3 * grown);
        sizes[j] = (0.2 + 0.14 * a.y) * grown;
    }
}

float Pool(float2 p, float grown, Kit kit)
{
    float angle = atan2(p.y, p.x);
    float dist = length(p) - (0.42 + Wobble(angle, kit.phase) * 1.5) * grown;
    for (int i = 0; i < 3; i++)
        dist = SmoothUnion(dist, length(p - kit.spots[i]) - kit.sizes[i], 0.16);

    return dist;
}

float Print(float2 p, float side)
{
    p.y *= side;
    float2 box = abs(p - float2(0.22, 0.0)) - float2(0.34, 0.2);
    float sole = length(max(box, 0.0)) + min(max(box.x, box.y), 0.0) - 0.22;
    float heel = length(p - float2(-0.58, 0.02)) - 0.27;
    float dist = min(sole, heel);
    float tread = 0.05 - abs(frac(p.x * 3.0) - 0.5);
    return max(dist, tread);
}

// a print keeps its shape in whole steps of two above its strength: 0 foot, 1 hand, 2 knee, 3 hoof, 4 tyre tread
float PrintKind(float code)
{
    return floor(abs(code) * 0.5);
}

float PrintStrength(float code)
{
    return abs(code) - 2.0 * PrintKind(code);
}

float Capsule(float2 q, float2 a, float2 b, float r)
{
    float2 ab = b - a;
    float t = saturate(dot(q - a, ab) / max(1e-5, dot(ab, ab)));
    return length(q - a - ab * t) - r;
}

float RoundBox(float2 q, float2 centre, float2 half, float r)
{
    float2 d = abs(q - centre) - half + r;
    return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0) - r;
}

float Taper(float2 q, float2 a, float2 b, float ra, float rb)
{
    float2 ab = b - a;
    float t = saturate(dot(q - a, ab) / max(1e-5, dot(ab, ab)));
    return length(q - a - ab * t) - lerp(ra, rb, t);
}

// a palm down with the fingers forward; side mirrors it for the other hand. The fingers stand a little apart from the palm and the
// hollow of the palm presses less, as in a real print
float Hand(float2 q, float side, float seed)
{
    // the shape has its thumb on +v (across = up x heading), which is a left hand: a right hand, side +1, flips it
    q.y *= -side;
    float palm = RoundBox(q, float2(-0.43, 0.0), float2(0.37, 0.35), 0.22);
    palm = SmoothUnion(palm, length((q - float2(-0.72, 0.17)) * float2(1.0, 1.15)) - 0.16, 0.08);
    float hollow = length((q - float2(-0.38, 0.06)) * float2(1.0, 1.25)) - 0.07 - 0.07 * Noise2(q * 8.0 + seed);
    palm = max(palm, -hollow * 0.5);
    float fingers = Taper(q, float2(0.06, 0.27), float2(0.66, 0.37), 0.088, 0.07);
    fingers = min(fingers, Taper(q, float2(0.08, 0.08), float2(0.83, 0.12), 0.092, 0.074));
    fingers = min(fingers, Taper(q, float2(0.06, -0.12), float2(0.75, -0.18), 0.088, 0.07));
    fingers = min(fingers, Taper(q, float2(0.0, -0.31), float2(0.48, -0.45), 0.077, 0.06));
    float thumb = Taper(q, float2(-0.5, 0.33), float2(-0.2, 0.62), 0.115, 0.092);
    thumb = min(thumb, length(q - float2(-0.13, 0.68)) - 0.085);
    // the tips press hardest; between the tips and the palm the fingers often print only in part
    float knuckle = Noise2(float2(q.x * 7.0, q.y * 3.0) + seed * 1.3) - 0.5;
    fingers = max(fingers, (0.02 - abs(q.x - 0.4 - knuckle * 0.06)) * (0.5 + knuckle));
    float dist = min(palm, min(fingers, thumb));
    return dist + (Noise2(q * 16.0 + seed) - 0.5) * 0.07;
}

// a knee through cloth: an oval pressed harder in the middle, with a faint weave in it
float Knee(float2 q, float seed)
{
    float dist = length(q / float2(0.95, 0.72)) - 1.0;
    float weave = sin((q.x + q.y) * 70.0) * sin((q.x - q.y) * 70.0);
    float blot = Noise2(q * 3.5 + seed) - 0.5;
    return max(dist * 0.7 + blot * 0.45, (weave - 0.82) * 0.06);
}

// a shod hoof: the shoe as an open ring with nail holes, the frog in the middle
float Hoof(float2 q)
{
    float ring = abs(length(q * float2(1.0, 1.08)) - 0.7) - 0.17;
    ring = max(ring, -0.5 - q.x);
    float angle = atan2(q.y, q.x);
    float nails = length(float2(frac(angle * 1.6) - 0.5, (length(q) - 0.7) * 3.0)) - 0.12;
    ring = max(ring, -nails * 0.4);
    float frog = Capsule(q, float2(-0.45, 0.0), float2(0.25, 0.0), 0.08 + 0.1 * saturate((0.25 - q.x) * 1.2));
    return min(ring, frog) + (Noise2(q * 14.0) - 0.5) * 0.08;
}

// a strip of tyre tread; the blocks follow the distance along the track, so pieces laid one after another line up
float Tread(float2 p, float along, float across)
{
    float edge = max(abs(p.x) - 1.0, abs(p.y) - 0.94);
    float chevron = frac(along * 7.0 + abs(across) * 5.0);
    float groove = 0.16 - abs(chevron - 0.5);
    float centre = 0.012 - abs(across);
    return max(edge, max(groove, centre) * 4.0);
}

float AnyPrint(float2 p, float code, float seed, float4 frame)
{
    float kind = PrintKind(code);
    float side = sign(code);
    if (kind < 0.5) return Print(p, side);

    float2 q = float2(p.x, p.y * frame.y / max(0.0001, frame.z));
    if (kind < 1.5) return Hand(q, side, seed);
    if (kind < 2.5) return Knee(q, seed);
    if (kind < 3.5) return Hoof(q);
    return Tread(p, seed + p.x / max(0.0001, frame.y), p.y / max(0.0001, frame.z));
}

float Grown(float4 life, float age)
{
    float fill = saturate(age / max(0.01, life.w));
    return 1.0 - (1.0 - fill) * (1.0 - fill) * (1.0 - fill);
}

float Shape(float2 p, float4 life, float seed, float age, float stretch, Kit kit, float4 frame)
{
    float splat = saturate(age / max(0.01, _SplatSeconds));
    float body = lerp(0.55, 1.0, 1.0 - (1.0 - splat) * (1.0 - splat));

    float dist;
    if (life.z > 1.5) dist = AnyPrint(p, life.w, seed, frame) + (1.0 - PrintStrength(life.w)) * (Noise2(p * 7.0 + seed * 7.0) * 0.6 - 0.12);
    else if (life.z > 0.5) dist = Pool(p, Grown(life, age), kit);
    else if (life.w > 4.5) dist = Smear(p, frame, life.w);
    else if (life.w > 3.5) dist = Run(p, seed, body, 1.0, age, kit, stretch);
    else if (life.w > 2.5) dist = Rays(p, seed, 18.0, 0.44, 0.54, 0.4 * body, 0.035, kit.phase);
    else if (life.w > 1.5) dist = Streak(p, seed, 1.0);
    else if (life.w > 0.5) dist = Rays(p, seed, 14.0, 0.38, 0.62, 0.32 * body, 0.04, kit.phase);
    else dist = Splash(p, stretch, body, kit);
    return dist;
}

#endif
