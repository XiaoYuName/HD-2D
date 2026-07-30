using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities2D.Extensions;
using Utilities2D;

namespace Slicer2D {
	public class ThinSliceBall : MonoBehaviour {
		private Vector2 direction;
		public float speed = 0.1f;

		public float radius = 1;

		static private List<ThinSliceBall> list = new List<ThinSliceBall>();

		static public List<ThinSliceBall> GetList() {
			return(list);
		}

		void OnEnable() {
			list.Add (this);
		}

		void OnDisable() {
			list.Remove (this);
		}

		void Start () {
			SetDirection(Random.insideUnitCircle);

			radius = GetComponent<CircleCollider2D>().radius;
		}
		
		void Update () {
			transform.Translate(direction * speed);

			BallToMapCollision();

			BallToBallsCollision();
			
			BallToSlicerCollision();
		}

		void SetDirection(Vector3 newDirection) {
			direction = newDirection;
			direction.Normalize();
		}

		public void BallToBallsCollision() {
			// Balls vs Balls Collision
			foreach(ThinSliceBall ball in ThinSliceBall.GetList()) {
				if (ball == this) {
					continue;
				}

				if (Vector2.Distance(transform.position, ball.transform.position) < ball.radius + radius) {
					ball.direction = Vector2D.RotToVec(Vector2D.Atan2(transform.position, ball.transform.position) - Mathf.PI).ToVector2();
					direction = Vector2D.RotToVec(Vector2D.Atan2(transform.position, ball.transform.position)).ToVector2();
					
					ball.transform.Translate(ball.direction * ball.speed);
					transform.Translate(direction * speed);
				}
			}
		}
		
		public void BallToMapCollision() {
			Vector2D position = new Vector2D(transform.position);

			// Balls vs Map Collisions
			foreach(Sliceable2D slicer in Sliceable2D.GetList()) {
				foreach (Pair2D id in Pair2D.GetList(slicer.shape.GetWorld().pointsList)) {
					if (Math2D.Circle.IntersectLine(id, position, radius) == true) {

						transform.Translate(direction * -speed);
						SetDirection(Math2D.ReflectAngle(direction, (float)Vector2D.Atan2(id.A, id.B)));
						transform.Translate(direction * speed);

					}
				}
			}
		}

		// Ball vs Slice Collision
		public void BallToSlicerCollision() {

			if (Math2D.Circle.IntersectLine(Slicer2DController.Get().linearControllerObject.GetPair(0).ToPair2D(), new Vector2D(transform.position), radius)) {
				ThinSlicerParticles.Create();

				// Remove Current Slicing Process
				Slicer2DController.Get().complexControllerObject.pointsList[0].Clear();
			}
		}
	}
}