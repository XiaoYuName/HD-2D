using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Utilities2D;

namespace Slicer2D {

	public class PointSlicerController : MonoBehaviour {
		public Layer sliceLayer = Layer.Create();

		public float visualScale = 1f;
		public float lineWidth = 1f;
		public float zPosition = 0f;
		public int slicesLimit = 3;
		
		public GameObject particlePrefab;
		private Color slicerColor = Color.white;

		private List<Pair2D> slicePairs = new List<Pair2D>();
		private Vector2D lastPoint = null;

		private SmartMaterial lineMaterial;
		private SmartMaterial lineMaterialBorder;

		private List<Pair2D> animationPairs = new List<Pair2D>();

		public Visuals visuals = new Visuals();

		void Start () {
			lineMaterial = MaterialManager.GetVertexLitCopy();
			lineMaterialBorder = MaterialManager.GetVertexLitCopy();
		}
		
		void Update () {
			Vector2D pos = GetMousePosition();

			if (UnityEngine.Input.GetMouseButtonDown(1)) {
				slicePairs.Clear();
				lastPoint = null;
			}

			// Puting point inside the Slice-able object is not allowed (!?!)
			if (PointInObjects(pos)) {
				slicerColor = Color.red;
			} else {
				slicerColor = Color.black;

				if (UnityEngine.Input.GetMouseButtonDown(0)) {
					if (lastPoint != null) {
						slicePairs.Add(new Pair2D(lastPoint, pos));
					}

					if (slicePairs.Count >= slicesLimit) {
						animationPairs = new List<Pair2D>(slicePairs);

						slicePairs.Clear();
						lastPoint = null;
					} else {
						lastPoint = pos;
					}
				}
			}

			foreach(Pair2D pair in slicePairs) {
				DrawLine(pair);
			}

			if (lastPoint != null) {
				DrawLine(new Pair2D(lastPoint, pos));
			}

			UpdateSliceAnimations();
		}

		public void UpdateSliceAnimations() {
			if (animationPairs.Count < 1) {
				return;

			}

			if (PointSlicerSlash.GetList().Count > 0) {
				return;
			}

			Pair2D animationPair = animationPairs.First();

			Slicing.LinearSliceAll(animationPair, sliceLayer);

			Vector3 position = animationPair.A.ToVector2();
			position.z = -1;

			GameObject particleGameObject = Instantiate(particlePrefab, position, Quaternion.Euler(0, 0, (float)Vector2D.Atan2(animationPair.A, animationPair.B) * Mathf.Rad2Deg));
			
			PointSlicerSlash particle = particleGameObject.GetComponent<PointSlicerSlash>();
			particle.moveTo = animationPair.B;

			animationPairs.Remove(animationPair);
		}

		public void DrawLine(Pair2D pair) {
			/*
			Mesh meshBorder = Slicer2DVisualsMesh.Linear.GenerateMesh(pair, transform, lineWidth * 2f * visualScale, zPosition + 0.001f,  0, lineWidth * 2f * visualScale, visuals.lineEndingType, visuals.lineEndingEdgeCount);
			Mesh mesh = Slicer2DVisualsMesh.Linear.GenerateMesh(pair, transform, lineWidth * visualScale, zPosition, 0, lineWidth * 2f * visualScale, visuals.lineEndingType, visuals.lineEndingEdgeCount);

			lineMaterial.SetColor ( Color.black);
			Max2DMesh.Draw( Slicer2DVisualsMesh.Linear.GenerateMesh(new Pair2D(pair.A, pair.A), transform, lineWidth * 10f * visualScale, zPosition + 0.001f, 0,  lineWidth * 10f * visualScale, visuals.lineEndingType, visuals.lineEndingEdgeCount), lineMaterialBorder.material);
			Max2DMesh.Draw( Slicer2DVisualsMesh.Linear.GenerateMesh(new Pair2D(pair.B, pair.B), transform, lineWidth * 10f * visualScale, zPosition + 0.001f, 0,  lineWidth * 10f * visualScale, visuals.lineEndingType, visuals.lineEndingEdgeCount), lineMaterialBorder.material);

			Max2DMesh.Draw(meshBorder, lineMaterialBorder.material);

			lineMaterial.SetColor ( slicerColor);
			Max2DMesh.Draw( Slicer2DVisualsMesh.Linear.GenerateMesh(new Pair2D(pair.A, pair.A), transform, lineWidth * 5f * visualScale, zPosition + 0.001f, 0, lineWidth * 5f * visualScale, visuals.lineEndingType, visuals.lineEndingEdgeCount), lineMaterial.material);
			Max2DMesh.Draw( Slicer2DVisualsMesh.Linear.GenerateMesh(new Pair2D(pair.B, pair.B), transform, lineWidth * 5f * visualScale, zPosition + 0.001f, 0, lineWidth * 5f * visualScale, visuals.lineEndingType, visuals.lineEndingEdgeCount), lineMaterial.material);

			Max2DMesh.Draw(mesh, lineMaterial.material);
			*/
		}

		public static Vector2D GetMousePosition() {
			return(new Vector2D (Camera.main.ScreenToWorldPoint (UnityEngine.Input.mousePosition)));
		}

		bool PointInObjects(Vector2D pos) {
			foreach (Sliceable2D slicer in Sliceable2D.GetList()) {
				if (slicer.shape.GetLocal().PointInPoly(pos.InverseTransformPoint(slicer.transform))) {
					return(true);
				}
			}
			return(false);
		}
	}
}