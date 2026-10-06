// BF Pixelizer meta.r 패킹 규약(계획 33) — 인코딩(PixelizedLit)과 디코딩(Composite)의 단일 출처.
//   meta.r = ObjectId(1~255) + 256 × OutlinePriority(0~7)
// 상한 2047은 ARGBHalf(binary16)가 정수를 오차 없이 담는 한계(2048) 안이다.
// ObjectId가 1 이상이므로 커버된 셀의 packed 값은 항상 1 이상이고 배경(Color.clear)은 0 →
// 기존의 `meta.r < 0.5 = 커버리지 없음` 판정이 우선권 도입 후에도 그대로 성립한다.
#ifndef BFPIXELIZER_META_INCLUDED
#define BFPIXELIZER_META_INCLUDED

#define BFP_PRIORITY_BASE   256.0
#define BFP_ID_MAX          255.0
#define BFP_PRIORITY_MAX    7.0

// 머티리얼 프로퍼티 → meta.r 값.
float BFP_PackMeta(float objectId, float priority)
{
    float id = clamp(round(objectId), 1.0, BFP_ID_MAX);
    float pri = clamp(round(priority), 0.0, BFP_PRIORITY_MAX);
    return id + BFP_PRIORITY_BASE * pri;
}

// meta.r → (id, priority). 비트 연산이라 부동소수 허용오차(구 0.25 비교)가 필요 없다.
// +0.5는 드라이버별 절삭 방향 차이를 흡수하는 방어값이다(half는 이 구간에서 정수가 정확).
void BFP_UnpackMeta(float packed, out uint id, out uint priority)
{
    uint v = (uint)(packed + 0.5); // 배경 = 0 → id = 0, priority = 0
    id = v & 255u;
    priority = v >> 8;
}

// 4탭 경계 판정 1탭분(계획 33).
// 현행 판정이 대칭적이라 ID가 다른 두 오브젝트가 맞닿으면 양쪽 셀이 모두 경계가 되어
// 2셀 두께의 이중선이 됐다. 우선권으로 비대칭화해 한쪽만 그리게 한다.
bool BFP_IsOutlineEdge(float neighborPacked, uint myId, uint myPriority)
{
    if (neighborPacked < 0.5)
        return true; // 배경 경계 — 실루엣 외곽은 우선권과 무관하게 언제나 그린다

    uint neighborId, neighborPriority;
    BFP_UnpackMeta(neighborPacked, neighborId, neighborPriority);

    if (neighborId == myId)
        return false; // 같은 오브젝트(같은 ID) — 내부

    // ID가 다른 경계: 우선권이 높거나 같을 때만 내가 그린다.
    // 동점(>=)이면 양쪽 다 참 → 기존 2셀 이중선이 보존된다(기본값 0끼리 = 현행 그대로).
    return myPriority >= neighborPriority;
}

#endif
