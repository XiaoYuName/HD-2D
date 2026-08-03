using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities2D;

namespace Slicer2D {
	public class ThinSliceHelper {

		// Check if polygon has ball objects inside
		public static bool PolygonHasBallsInside(Polygon2D poly) {
			foreach(ThinSliceBall ball in ThinSliceBall.GetList()) {
				if (poly.PointInPoly(new Vector2D(ball.transform.position)) == true) {
					return(true);
				}
			}
			return(false);
		}

		// After Slice - Get smallest polygon which does not have balls in it
		public static GameObject GetSmallestGameObject(Slice2D sliceResult) {
			double area = 1e+10f;
			GameObject CutObject = null;
			
			foreach(GameObject resultObject in sliceResult.GetGameObjects()) {
				Polygon2D poly = Polygon2DList.CreateFromGameObject(resultObject)[0];
				if (poly.GetArea() < area && PolygonHasBallsInside(poly.ToWorldSpace(resultObject.transform)) == false) {
					CutObject = resultObject;
					area = poly.GetArea();
				}
			}

			return(CutObject);
		}

		// Before Slice - Get smallest polygon which does not have balls in it
		public static Polygon2D GetSmallestPolygon(Slice2D sliceResult) {
			double area = 1e+10f;
			Polygon2D CutObject = null;

			foreach(Polygon2D poly in sliceResult.GetPolygons()) {
				if (poly.GetArea() < area && PolygonHasBallsInside(poly) == false) {
					CutObject = poly;
					area = poly.GetArea();
				}
			}

			return(CutObject);
		}

		// Polygon Removal
		static int cutObjects = 0;

		static public void ExplodeGameObject(GameObject CutObject, GameObject originObject) {
			Sliceable2D slicerA = CutObject.GetComponent<Sliceable2D>();
			Sliceable2D slicerB = originObject.GetComponent<Sliceable2D>();

			Polygon2D polyA = slicerA.shape.GetWorld();
			Polygon2D polyB = slicerB.shape.GetWorld();

			Rect boundsA = polyA.GetBounds();
			Rect boundsB = polyB.GetBounds();

			Vector2D centerA = new Vector2D(boundsA.center);
			Vector2D centerB = new Vector2D(boundsB.center);

			double direction = (double)Vector2D.Atan2(centerA, centerB);

			Rigidbody2D rigidBody2D = CutObject.AddComponent<Rigidbody2D>();

			rigidBody2D.AddForce(Vector2D.RotToVec(direction).ToVector2() * 200);

			rigidBody2D.AddTorque(Random.Range(-15, 15));

			cutObjects ++;
			CutObject.transform.Translate(0, 0, 100 - cutObjects + CutObject.transform.position.z);

			CutObject.AddComponent<Mesh2D>().material = slicerA.materialSettings.material;

			UnityEngine.Object.Destroy(CutObject.GetComponent<Sliceable2D>());
			UnityEngine.Object.Destroy(CutObject.GetComponent<ThinSliceRules>());
		}
	}
}