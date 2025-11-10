using UnityEngine;

[System.Serializable]
public class WeaponData
{
    public string name = "Weapon";
    public WeaponType weaponType = WeaponType.Primary;

    [Header("Fire")]
    public bool isAuto = true;
    public float fireRate = 0.15f;      // 초 단위 최소 발사 간격
    public bool useHitscan = true;
    public float range = 100f;
    public LayerMask hitMask;           // Raycast 검사 대상 레이어

    [Header("Damage")]
    public int baseDamage = 25;
    public float headMult = 1.5f;

    [Header("Magazine (magazine-based)")]
    public int magSize = 30;
    public int reserveMags = 3;         // 기본 제공 탄창 개수(게임 중 추가 없음)
    public float reloadTime = 1.8f;

    [Header("FX & Audio (optional)")]
    public GameObject hitFx;
    public AudioClip fireSfx;
    public AudioClip reloadSfx;
}