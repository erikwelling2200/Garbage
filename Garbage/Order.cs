using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Garbage {
	class Order {
		public int orderID, frequence, containerVolume, matrixID;
		public float duration;
		public Order (int orderID, int frequence, int containerVolume, float duration, int matrixID) {
			this.orderID = orderID;
			this.frequence = frequence;
			this.containerVolume = containerVolume;
			this.duration = duration;
			this.matrixID = matrixID;
		}
		public Order () {
			orderID = 0;
			frequence = 0;
			containerVolume = 0;
			duration = 0;
			matrixID = Program.dumpID;
		}
	}
	class Loop {
		public int truck, day;
		public List<Order> orders;
		public Loop (int truck, int day) {
			this.truck = truck;
			this.day = day;
			orders = new List<Order>();
		}
		public void Add (Order order) {
			orders.Add(order);
		}
		public void AddDump () {
			orders.Add(new Order());
		}

	}
	class Node {
		public List<Loop> loops;
		Tuple<float, float> score = null;
		public Node(List<Loop> loops) {
			this.loops = loops;
		}
		public Tuple<float, float> GetScore() {
			if (score == null) {
				score = Program.CalculateScore(this);
			}
			return score;
		}
	}
}
