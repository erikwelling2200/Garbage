using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Garbage {
	struct Order {
		int orderID, frequence, containerVolume, matrixID;
		float duration;
		public Order (int orderID, int frequence, int containerVolume, float duration, int matrixID) {
			this.orderID = orderID;
			this.frequence = frequence;
			this.containerVolume = containerVolume;
			this.duration = duration;
			this.matrixID = matrixID;
		}
	}
}
