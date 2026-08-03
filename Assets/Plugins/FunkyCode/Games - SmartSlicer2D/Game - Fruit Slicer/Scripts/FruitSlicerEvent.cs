using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Slicer2D {
	
	public class FruitSlicerEvent : MonoBehaviour {

		void Start () {
			Sliceable2D slicer = GetComponent<Sliceable2D>();
			slicer.AddResultEvent(SliceEvent);
		}
		
		void SliceEvent(Slice2D slice){
			FruitSlicerGameManager.instance.score += 15;

			foreach(GameObject g in slice.GetGameObjects()) {
				Vector3 pos = g.transform.position;
				pos.z = Random.Range(pos.z, 50);
				g.transform.position = pos;

				Rigidbody2D rb = g.GetComponent<Rigidbody2D>();
				rb.AddForce(new Vector2(Random.Range(-200, 200), Random.Range(100, 200)));
				rb.AddTorque(Random.Range(-100, 100));

				//PolygonCollider2D collider = g.GetComponent<PolygonCollider2D>();
				//collider.isTrigger = false;

				Sliceable2D slicer = g.GetComponent<Sliceable2D>();
				slicer.enabled = false;

				slicer.gameObject.AddComponent<FruitSlicerFadeAway>();

				//ColliderLineRenderer2D lineRenderer = g.GetComponent<ColliderLineRenderer2D>();
				//lineRenderer.customColor = true;
				//lineRenderer.color = Color.red;
				//lineRenderer.lineWidth = 0.5f;
			}
		}
	}
	
}