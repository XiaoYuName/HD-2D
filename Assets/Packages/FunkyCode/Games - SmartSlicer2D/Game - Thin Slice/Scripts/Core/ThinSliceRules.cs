using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Utilities2D;

namespace Slicer2D {
	public class ThinSliceRules : MonoBehaviour {

		void Start () {
			Sliceable2D.AddGlobalEvent(OnSlice);
			Sliceable2D.AddGlobalResultEvent(AfterSlice);
		}
		
		// Triggered Before Every Slice 
		// Should we perform slice? Is it succesful according our rules?
		bool OnSlice(Slice2D sliceResult) {
			Polygon2D CutObject = ThinSliceHelper.GetSmallestPolygon(sliceResult);

			// Forbidden double slices // Shouldn't be possible with linear slicer
			if (sliceResult.GetPolygons().Count > 2) {
				return(false);
			}

			// Add Particles if slice is succesful
			if (CutObject == null) {
				ThinSlicerParticles.Create();
				Slicer2DController.Get().complexControllerObject.pointsList[0].Clear();
				return(false);
			}

			return(true);	
		}

		// Triggered On Every Successful Slice
		// Manage Game Objects
		void AfterSlice(Slice2D sliceResult) {
			GameObject cutObject = ThinSliceHelper.GetSmallestGameObject(sliceResult);
			
			if (cutObject != null) {
				ThinSliceHelper.ExplodeGameObject(cutObject, sliceResult.originGameObject);

				foreach(GameObject g in sliceResult.GetGameObjects()) {
					if (g != cutObject) {
						g.name = sliceResult.originGameObject.name;
					} else {
						g.name = "Falling Peace";
					}
				}
			}

			ThinSliceGameManager.UpdateLevelProgress();
		}
	}
}