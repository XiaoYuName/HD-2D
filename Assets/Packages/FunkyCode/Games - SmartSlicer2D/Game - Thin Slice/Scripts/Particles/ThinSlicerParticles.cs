using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utilities2D;

namespace Slicer2D {

	public class ThinSlicerParticles {

		static public void Create() {
			if (Slicer2DController.Get().linearControllerObject.startedSlice == false) {
				return;
			}

			Vector2List points = Controller.Linear.Controller.GetLinearVertices(Slicer2DController.Get().linearControllerObject.GetPair(0),  Slicer2DController.Get().linearControllerObject.minVertexDistance);
			
			if (points.Count() < 3) {
				return;
			}

	
			float size = 0.5f;
			Vector2 f = points.First();
			f.x -= size / 2;
			f.y -= size / 2;

			List<Vector2D> list = new List<Vector2D>();
			list.Add( new Vector2D (f.x, f.y));
			list.Add( new Vector2D (f.x + size, f.y));
			list.Add( new Vector2D (f.x + size, f.y + size));
			list.Add( new Vector2D (f.x, f.y + size));
			list.Add( new Vector2D (f.x, f.y));

			f = points.Last();
			f.x -= size / 2;
			f.y -= size / 2;

			list = new List<Vector2D>();
			list.Add( new Vector2D (f.x, f.y));
			list.Add( new Vector2D (f.x + size, f.y));
			list.Add( new Vector2D (f.x + size, f.y + size));
			list.Add( new Vector2D (f.x, f.y + size));
			list.Add( new Vector2D (f.x, f.y));
			

		}
	}
}