using Unity.Cinemachine;
using UnityEngine;

public class ScreenShake : MonoBehaviour
{
	private CinemachineImpulseSource cinemachineImpulseSource;

	public static ScreenShake Instance { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Debug.LogError("There's more than one ScreenShake! " + transform + " - " + Instance);
			Destroy(gameObject);
		}
		else
		{
			Instance = this;
			DontDestroyOnLoad(gameObject);
			cinemachineImpulseSource = GetComponent<CinemachineImpulseSource>();
		}
	}

	public void Shake(float intensity = 0.5f)
	{
		cinemachineImpulseSource.GenerateImpulse(intensity);
	}
}
