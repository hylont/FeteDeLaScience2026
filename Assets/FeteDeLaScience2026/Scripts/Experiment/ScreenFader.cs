using System.Collections;
using UnityEngine;

// Fades a full-screen CanvasGroup (a black Image parented to the HMD camera) to opaque.
public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup _fadeCanvasGroup;
    [SerializeField] private float _fadeDuration = 2f;

    public void FadeToBlack()
    {
        if (_fadeCanvasGroup == null)
        {
            LLogger.E("ScreenFader has no CanvasGroup assigned.");
            return;
        }

        StartCoroutine(FadeCoroutine());
    }

    private IEnumerator FadeCoroutine()
    {
        float elapsed = 0f;
        float startAlpha = _fadeCanvasGroup.alpha;

        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            _fadeCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, elapsed / _fadeDuration);
            yield return null;
        }

        _fadeCanvasGroup.alpha = 1f;
    }
}
