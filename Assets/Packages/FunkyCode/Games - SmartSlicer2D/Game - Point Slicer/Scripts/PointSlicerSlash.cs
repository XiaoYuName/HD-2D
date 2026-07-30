using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities2D;

public class PointSlicerSlash : MonoBehaviour {
	public Vector2D moveTo;
	public float speed = 1f;

	static public List<PointSlicerSlash> GetList() {
		List<PointSlicerSlash> result = new List<PointSlicerSlash>();
		foreach (PointSlicerSlash buffer in Object.FindObjectsOfType(typeof(PointSlicerSlash))) {
			result.Add(buffer);
		}
		return(result);
	}

	void Update () {
		Vector3 pos = transform.position;

		if (Vector2.Distance(moveTo.ToVector2(), pos) > speed * 1.5f) {
			pos += Vector2D.RotToVec(Vector2D.Atan2(moveTo, new Vector2D(pos))).ToVector3(0) * speed;
			pos.z = -3f;
			transform.position = pos;
		} else {
			Destroy(gameObject);
		}
	}
}
