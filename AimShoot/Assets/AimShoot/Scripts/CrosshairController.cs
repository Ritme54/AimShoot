using UnityEngine;
using UnityEngine.UI;

public class CrosshairController : MonoBehaviour
{
    [Header("UI")]
    public Canvas canvas;                    // Screen Space - Overlay인 Canvas를 할당
    public RectTransform crosshairRect;      // Crosshair Image의 RectTransform
    public Image crosshairImage;             // Crosshair Image 컴포넌트 (optional)

    [Header("Sprites")]
    public Sprite primaryCrosshairSprite;
    public Sprite secondaryCrosshairSprite;
    public Sprite hiddenCrosshairSprite;     // 필요시 사용 (빈 상태)

    [Header("Options")]
    public bool hideSystemCursor = false;    // true면 시스템 커서 숨김

    void Awake()
    {
        if (canvas == null)
        {
            canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                Debug.LogWarning("[CrosshairController] Canvas가 할당되지 않았습니다. 인스펙터에서 Canvas를 지정하세요.");
        }

        if (crosshairRect == null && crosshairImage != null)
            crosshairRect = crosshairImage.rectTransform;

        if (hideSystemCursor)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.None; // 테스트 시에는 None 권장
        }
    }

    void Update()
    {
        UpdatePosition();
    }

    void UpdatePosition()
    {
        if (canvas == null || crosshairRect == null) return;

        Vector2 screenPos = Input.mousePosition;

        // Screen Space - Overlay용 변환 (camera는 null)
        RectTransform canvasRect = canvas.transform as RectTransform;
        Vector2 anchoredPos;
        bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPos,
            null, // Overlay 모드일 때는 null
            out anchoredPos);

        if (success)
        {
            crosshairRect.anchoredPosition = anchoredPos;
        }
    }

    // 무기 유형에 따라 스프라이트 전환
    public void SetCrosshair(WeaponType type)
    {
        if (crosshairImage == null)
        {
            Debug.LogWarning("[CrosshairController] crosshairImage가 할당되지 않았습니다.");
            return;
        }

        switch (type)
        {
            case WeaponType.Primary:
                crosshairImage.sprite = primaryCrosshairSprite;
                crosshairImage.enabled = (primaryCrosshairSprite != null);
                break;
            case WeaponType.Secondary:
                crosshairImage.sprite = secondaryCrosshairSprite;
                crosshairImage.enabled = (secondaryCrosshairSprite != null);
                break;
            default:
                crosshairImage.sprite = hiddenCrosshairSprite;
                crosshairImage.enabled = (hiddenCrosshairSprite != null);
                break;
        }
    }
    

public Vector3 GetWorldAimPoint(float defaultDistance = 50f, LayerMask mask = default)
    {
        Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, crosshairRect.position);
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit, defaultDistance, mask)) return hit.point;
        return ray.GetPoint(defaultDistance);
    }
    public void HideCrosshair()
    {
        if (crosshairImage != null) crosshairImage.enabled = false;
    }
}