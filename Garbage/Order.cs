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
		public List<SubLoop> subLoops;
		//public List<Order> orders;
		public Loop (int truck, int day) {
			this.truck = truck;
			this.day = day;
			subLoops = new List<SubLoop>();
			//orders = new List<Order>();
		}
		public void Add (Order order) {
			subLoops [subLoops.Count - 1].orders.Add(order);
			//orders.Add(order);
		}
		public Tuple<float, List<int>> GetTimeAndVolume () {
			Tuple<float, List<int>> result = new Tuple<float, List<int>>(0, new List<int>());
			foreach (SubLoop subLoop in subLoops) {
				Tuple<float, int>  subResult = subLoop.GetTimeAndVolume();
				result.Item2.Add(subResult.Item2);
				result = new Tuple<float, List<int>>(result.Item1 + subResult.Item1, result.Item2);
			}
			return result;
		}

		//public void AddDump () {
		//	orders.Add(new Order());
		//}
	}
	class SubLoop {
		public List<Order> orders = new List<Order>();
		public SubLoop untangledVersion = null;
		Tuple<float, int> timeAndVolume = null;

		public SubLoop GetUntangledVersion () {
			if (untangledVersion == null) {
				untangledVersion = Program.SolveTravellingSM(this).Item1;
				int dumpIndex = 0;
				for (int i = 0; i < untangledVersion.orders.Count; i++) {
					if (untangledVersion.orders[i].orderID == 0) {
						dumpIndex = i;
					}
				}
				untangledVersion = Program.OffsetSubLoop(untangledVersion, -(dumpIndex + 1));
			}
			return untangledVersion;
		}
		public Tuple<float, int> GetTimeAndVolume () {
			if (timeAndVolume == null) {
				timeAndVolume = Program.CalculateLoopDurationAndCapacity(this);
			}
			return timeAndVolume;
		}
	}
	class Node {
		public Loop [,] loops;
		public Tuple<float, float> score = null;
		public Dictionary<int, List<int>> pickupDays = null;
		public Node () {
			loops = new Loop [2, 5];
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					loops [truck-1, day-1] = new Loop(truck, day);
				}
			}
		}
		public Tuple<float, float> GetScore () {
			if (score == null) {
				score = Program.CalculateScore(this);
			}
			return score;
		}
		public Dictionary<int, List<int>> GetPickupDays () {
			if (pickupDays == null) {
				pickupDays = CalculatePickupDays();
			}
			return pickupDays;
		}
		public Dictionary<int, List<int>> CalculatePickupDays () {
			Dictionary<int, List<int>> pds = new Dictionary<int, List<int>>();
			for (int i = 0; i < Program.completeOrderList.Count; i++) {
				pds.Add(Program.completeOrderList [i].orderID, new List<int>());
			}
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					for (int i = 0; i < loops [truck - 1, day - 1].subLoops.Count; i++) {
						for (int x = 0; x < loops [truck - 1, day - 1].subLoops [i].orders.Count - 1; x++) {
							Order order = loops [truck - 1, day - 1].subLoops [i].orders [x];
							if (order.orderID != 0) {
								pds [order.orderID].Add(day);
							}
						}
					}
				}
			}
			return pds;
		}
	}
	class Neighbour {
		public Node origin;
		public SubLoop newSubLoop;
		public int truck, day;
		public int subLoopIndex;
		Tuple<float, float> score = null;
		Dictionary<int, List<int>> pickupDays = null;

		public Neighbour (Node origin, SubLoop newSubLoop, int truck, int day, int subLoopIndex) {
			this.origin = origin;
			this.newSubLoop = newSubLoop;
			this.truck = truck;
			this.day = day;
			this.subLoopIndex = subLoopIndex;
		}
		public Tuple<float, float> GetScore () {
			if (score == null) {
				score = Program.CalculateScore(this);
			}
			return score;
		}
		public Dictionary<int, List<int>> GetPickupDays () {
			if (pickupDays == null) {
				pickupDays = CalculatePickupDays();
			}
			return pickupDays;
		}
		public Dictionary<int, List<int>> CalculatePickupDays () {
			Dictionary<int, List<int>> pds = new Dictionary<int, List<int>>(origin.GetPickupDays());
			if (subLoopIndex < origin.loops [truck - 1, day - 1].subLoops.Count){
				SubLoop oldSubLoop = origin.loops [truck - 1, day - 1].subLoops [subLoopIndex];
				for (int i = 0; i < oldSubLoop.orders.Count - 1; i++) {
					if (oldSubLoop.orders[i].orderID != 0) {
						List<int> newList = new List<int>(pds [oldSubLoop.orders [i].orderID]);
						newList.Remove(day);
						pds [oldSubLoop.orders [i].orderID] = newList;
					}
				}
			}
			if (newSubLoop != null) {
				for (int i = 0; i < newSubLoop.orders.Count - 1; i++) {
					if (newSubLoop.orders [i].orderID != 0) {
						List<int> newList = new List<int>(pds [newSubLoop.orders [i].orderID]);
						newList.Add(day);
						pds [newSubLoop.orders [i].orderID] = newList;
					}
				}
			}
			return pds;
		}

		public Node ToNode () {
			Loop loop = origin.loops [truck - 1, day - 1];
			if (newSubLoop == null) {
				loop.subLoops.RemoveAt(subLoopIndex);
			} else {
				if (loop.subLoops.Count <= subLoopIndex) {
					loop.subLoops.Add(newSubLoop);
				} else {
					SubLoop subLoop1 = new SubLoop();
					SubLoop subLoop2 = null;
					SubLoop currentSubLoop = subLoop1;
					for (int i = 0; i < newSubLoop.orders.Count - 1; i++) {
						currentSubLoop.orders.Add(newSubLoop.orders [i]);
						if (newSubLoop.orders[i].orderID == 0) {
							subLoop2 = new SubLoop();
							currentSubLoop = subLoop2;
						}
					}
					currentSubLoop.orders.Add(newSubLoop.orders [newSubLoop.orders.Count - 1]);
					loop.subLoops [subLoopIndex] = subLoop1;
					if (subLoop2 != null) {
						loop.subLoops.Add(subLoop2);
					} else {
						subLoop1.untangledVersion = newSubLoop.untangledVersion;
					}
				}
			}
			origin.score = score;
			origin.pickupDays = pickupDays;
			return origin;
		}
	}
}
