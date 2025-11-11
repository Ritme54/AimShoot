using UnityEngine;

[System.Serializable]
public class WeaponData
{
    [Header("Identity")]
    public string name = "Weapon";
    public WeaponType weaponType = WeaponType.Primary;

    [Header("Fire")]
    public bool isAuto = true;
    public float fireRate = 0.15f;      // 초 단위 최소 발사 간격
    public bool useHitscan = true;      // hitscan 전용(프로젝트타입 필드 제거)
    public float range = 100f;
    public LayerMask hitMask;           // Raycast 검사 대상 레이어 (발사 판정용)

    [Header("Spread (Hitscan)")]
    public float spreadRadius = 0.0f;   // 조준점 기준 반지름(m 단위). 0이면 퍼짐 없음.

    [Header("Damage")]
    public int baseDamage = 25;
    public float headMult = 1.5f;       // 헤드샷 배수

    [Header("Head detection")]
    public bool headIsTag = true;       // true면 헤드 판정은 태그(headTag)로, false면 레이어로 판정
    public string headTag = "Head";     // headIsTag==true일 때 사용(CompareTag용)
    public string targetRootComponentName = "Targets"; // Targets 컴포넌트 이름(찾기용)

    [Header("Magazine (magazine-based)")]
    public int magSize = 30;
    public int reserveMags = 3;
    public float reloadTime = 1.8f;

    [Header("FX & Audio")]
    public GameObject muzzleFx;
    public Vector3 muzzleFxLocalPos = Vector3.zero;
    public float muzzleFxScale = 1f;
    public float muzzleFxLifetime = 0.12f; // 인스펙터로 조정 가능
    public GameObject impactFx;
    public float impactFxScale = 1f;
    public float impactFxLifetime = 1f; // 이미 있음

    public AudioClip fireSfx;
    public AudioClip reloadSfx;

    [Header("Pooling / Playback")]
    public bool useMuzzlePooling = true;
    public bool useImpactPooling = true;

    [Header("Misc")]
    public float recoilAmount = 1.0f;
    public string description = "";
}