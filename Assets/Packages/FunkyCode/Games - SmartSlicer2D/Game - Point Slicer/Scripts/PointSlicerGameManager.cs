using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Utilities2D;

namespace Slicer2D {

	public class SlicePointGameManager : MonoBehaviour {
		public double startingArea = 0;
		public double currentArea = 0;

		public Text percentText;
		
		void Start () {
			
			UpdateCurrentArea();

			startingArea = currentArea;
		}
		
		void Update () {
			Polygon2D cameraPolygon = Polygon2D.CreateFromCamera(Camera.main);
			cameraPolygon = cameraPolygon.ToRotation(Camera.main.transform.rotation.eulerAngles.z * Mathf.Deg2Rad);
			cameraPolygon = cameraPolygon.ToOffset(new Vector2D(Camera.main.transform.position));
		
			foreach(Sliceable2D slicer in Sliceable2D.GetListCopy()) {
				if (Math2D.PolyCollidePoly(slicer.shape.GetWorld(), cameraPolygon) == false) {
					Destroy(slicer.gameObject);
				}
			}

			UpdateCurrentArea();
	
			int percent = (int)((currentArea / startingArea) * 100);
			percentText.text = "Left: " + percent + "%";
		}

		public void UpdateCurrentArea() {
			currentArea = 0f;
			foreach(Sliceable2D slicer in Sliceable2D.GetListCopy()) {
				currentArea += slicer.shape.GetLocal().GetArea();
			}
		}
	}
	
}