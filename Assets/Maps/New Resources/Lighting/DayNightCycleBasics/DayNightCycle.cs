using UnityEngine;
using System.Collections;

public class DayNightCycle : MonoBehaviour 
{
		[Range(0.0f, 1.0f)]
		public float time;
		public float fullDayLength;
		public float startTime = 0.4f;
		private float timeRate;
		public Vector3 noon;

		[Header("sun")]
		public Light sun;
		public Gradient sunColor;
		public AnimationCurve sunIntensity;
	    
	[Header("moon")]
	public Light moon;
	public Gradient moonColor;
	public AnimationCurve moonIntensity;

	void Start ()
	{
		timeRate = 1.0f / fullDayLength;
		time = startTime;
	}
	void Update ()
	{
		// increment time
		time += timeRate * Time.deltaTime;

		if (time >= 1.0f)
			time = 0.0f;

		// light rotation
		sun.transform.eulerAngles = (time - 0.25f) * noon * 4.0f;
		moon.transform.eulerAngles = (time - 0.75f) * noon * 4.0f;

		//light intensity
		sun.intensity = sunIntensity.Evaluate (time);
		moon.intensity = moonIntensity.Evaluate (time);
	}

}
