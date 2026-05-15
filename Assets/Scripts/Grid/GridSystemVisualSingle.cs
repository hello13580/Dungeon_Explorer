using System.Collections;
using UnityEngine;

public class GridSystemVisualSingle : MonoBehaviour
{
	[SerializeField]
	private MeshRenderer meshRenderer;

	[SerializeField]
	private float fadeDuration = 0.25f;

	public void InstantHide()
	{
		meshRenderer.enabled = false;
	}

	public void InstantShow(Material material)
	{
		StopAllCoroutines();
		meshRenderer.material = material;
		Color color = meshRenderer.material.color;
		color.a = 1f;
		meshRenderer.material.color = color;
		meshRenderer.enabled = true;
	}

	public void FadeShow(Material material)
	{
		meshRenderer.material = material;
		meshRenderer.enabled = true;
		StopAllCoroutines();
		StartCoroutine(Fade(0f, 1f));
	}

	public void FadeHide()
	{
		StopAllCoroutines();
		StartCoroutine(Fade(1f, 0f));
	}

	private IEnumerator Fade(float startAlpha, float endAlpha)
	{
		float timer = 0f;
		Color color = meshRenderer.material.color;
		while (timer < fadeDuration)
		{
			timer += Time.deltaTime;
			float a = Mathf.Lerp(startAlpha, endAlpha, timer / fadeDuration);
			color.a = a;
			meshRenderer.material.color = color;
			yield return null;
		}
		color.a = endAlpha;
		meshRenderer.material.color = color;
		if (endAlpha == 0f)
		{
			meshRenderer.enabled = false;
		}
	}
}
