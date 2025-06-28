using UnityEngine;
using UnityEngine.UI;

public class CameraFadeIn : MonoBehaviour
{
    public Image fadeImage; // Inspectorで黒Imageをアサイン
    public float fadeDuration = 1.5f;

    void Start()
    {
        if (fadeImage != null)
            StartCoroutine(FadeIn());
    }

    System.Collections.IEnumerator FadeIn()
    {
        float t = 0f;
        Color c = fadeImage.color;
        c.a = 1f;
        fadeImage.color = c;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(1f, 0f, t / fadeDuration);
            fadeImage.color = c;
            yield return null;
        }
        c.a = 0f;
        fadeImage.color = c;
        fadeImage.gameObject.SetActive(false); // 完全に消す
    }
}