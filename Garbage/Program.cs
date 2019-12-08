using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Garbage {
	class Program {
		public static int [,] distances;
		public static int dumpID = 287, dumpDuration = 30;
		public static List<Order> completeOrderList = new List<Order>();
		public static Random random = new Random();

		static void Main (string [] args) {
			List<Order> orders = new List<Order>();
			List<Loop> loops = new List<Loop>();

			//read order file
			int maxMatrixID = 0;
			StreamReader orderFile = new StreamReader("Orderbestand.txt");
			string line = orderFile.ReadLine();
			while (line != null) {
				line = orderFile.ReadLine();
				if (line != null) {
					string [] parameters = line.Split(';');
					int orderID = int.Parse(parameters [0]);
					int frequence = int.Parse(parameters [2].Substring(0, 1));
					int containerVolume = int.Parse(parameters [3]) * int.Parse(parameters [4]);
					float duration = float.Parse(parameters [5]);
					int matrixID = int.Parse(parameters [6]);
					if (matrixID > maxMatrixID) {
						maxMatrixID = matrixID;
					}
					if (frequence == 1) {
						orders.Add(new Order(orderID, frequence, containerVolume, duration, matrixID)); //add order to list
					} else {
						completeOrderList.Add(new Order(orderID, frequence, containerVolume, duration, matrixID));
					}
				}
			}
			completeOrderList.AddRange(orders);

			distances = new int [maxMatrixID + 1, maxMatrixID + 1];
			StreamReader distanceFile = new StreamReader("AfstandenMatrix.txt");
			line = distanceFile.ReadLine();
			while (line != null) {
				line = distanceFile.ReadLine();
				if (line != null) {
					string [] parameters = line.Split(';');
					int matrixID1 = int.Parse(parameters [0]);
					int matrixID2 = int.Parse(parameters [1]);
					int distance = int.Parse(parameters [3]);
					distances [matrixID1, matrixID2] = distance;
				}
			}

			int currentLocation = dumpID;
			List<Order> todayOrders = new List<Order>();
			for (int day = 1; day <= 5; day++) {
				for (int truck = 1; truck <= 2; truck++) {
					Loop pickupLoop = new Loop(truck, day);
					float timeLeft = 12f * 60f;
					float currentCapacity = 20000;
					todayOrders.AddRange(orders);
					while (todayOrders.Count > 0) {
						for (int i = todayOrders.Count - 1; i >= 0; i--) {
							Order order = todayOrders [i];
							if (timeLeft < GetDistance(currentLocation, order.matrixID) + order.duration + GetDistance(order.matrixID, dumpID) + dumpDuration) {
								todayOrders.RemoveAt(i);
							}
						}
						List<Order> currentOrders = new List<Order>();
						List<Order> loop = new List<Order>();
						currentOrders.AddRange(todayOrders);
						float heuristicSum = 0, loopduration = 0, loopCapacity = 0;
						int loopPos = currentLocation;
						while (currentOrders.Count > 0) {
							float bestHeuristic = float.NegativeInfinity;
							Order bestOrder = currentOrders [0];
							for (int i = currentOrders.Count - 1; i >= 0; i--) {
								Order order = currentOrders [i];
								float newHeuristic;
								bool enoughCapacity = currentCapacity >= loopCapacity + order.containerVolume * 0.2f;
								bool enoughTime = timeLeft >= loopduration + GetDistance(loopPos, order.matrixID) + order.duration + GetDistance(order.matrixID, dumpID) + dumpDuration;
								if (enoughCapacity && enoughTime) {
									newHeuristic = 2f * order.duration - GetDistance(loopPos, order.matrixID);
									if (newHeuristic > bestHeuristic) {
										bestHeuristic = newHeuristic;
										bestOrder = order;
									}
								} else {
									currentOrders.Remove(order);
								}
							}
							if (currentOrders.Count > 0) {
								heuristicSum += bestHeuristic;
								loop.Add(bestOrder);
								loopduration += GetDistance(loopPos, bestOrder.matrixID);
								loopduration += bestOrder.duration;
								loopPos = bestOrder.matrixID;
								loopCapacity += bestOrder.containerVolume * 0.2f;
								currentOrders.Remove(bestOrder);
							}
						}
						heuristicSum -= (GetDistance(loopPos, dumpID) + dumpDuration);
						//Console.WriteLine("SUM: " + heuristicSum);
						if (heuristicSum > 0 && loop.Count > 0) {
							for (int i = 0; i < loop.Count; i++) {
								Order order = loop [i];
								//collect garbage
								timeLeft -= GetDistance(currentLocation, order.matrixID); //move to order
								currentLocation = order.matrixID;
								timeLeft -= order.duration; //collect garbage
								currentCapacity -= order.containerVolume * 0.2f; //lose capacity
								todayOrders.Remove(order);
								orders.Remove(order);

								pickupLoop.Add(order);
							}

							//empty
							timeLeft -= GetDistance(currentLocation, dumpID); //move to dump
							currentLocation = dumpID; //set location to dump
							timeLeft -= dumpDuration; //empty
							currentCapacity = 20000; //reset capacity

							pickupLoop.AddDump();
						} else {
							todayOrders.Clear();
						}
					}
					loops.Add(pickupLoop);
				}
			}
			Tuple<float, float> bestScore = CalculateScore(loops);

			float T = 2;
			int a = 1;
			while (T > 0.15f) {
				for (int i = 0; i < 25; i++) {
					Console.WriteLine(a + " / " + 2275 + ": " + (bestScore.Item1 + bestScore.Item2));
					a++;
					List<List<Loop>> neighbours = GetNeighbours(loops);
					bool accepted = false;
					List<Loop> neighbour = null;
					Tuple<float, float> score = null;
					while (accepted == false) {
						neighbour = neighbours [random.Next(neighbours.Count)];
						score = CalculateScore(neighbour);
						float scoreSum = score.Item1 + score.Item2;
						if (scoreSum < bestScore.Item1 + bestScore.Item2) {
							accepted = true;
						} else {
							float delta = scoreSum - (bestScore.Item1 + bestScore.Item2);
							double p = Math.Exp(-delta / T);
							//Console.WriteLine(p);
							double r = random.NextDouble();
							//Console.WriteLine(r);
							if (r < p) {
								accepted = true;
							}
						}
					}
					loops = neighbour;
					bestScore = score;
				}
				T *= 0.95f;
			}
			PrintResult(loops);
			Console.WriteLine("Traveling time: " + bestScore.Item1);
			Console.WriteLine("Decline penalty: " + bestScore.Item2);
			Console.WriteLine("Total score: " + (bestScore.Item1 + bestScore.Item2));
			Console.Read();
		}
		public static float GetDistance (Order a, Order b) {
			return GetDistance(a.matrixID, b.matrixID);
		}
		public static float GetDistance (int matrixID1, int matrixID2) {
			return (float)distances [matrixID1, matrixID2] / 60f;
		}
		public static void PrintResult (List<Loop> loops) {
			foreach (Loop loop in loops) {
				for (int i = 0; i < loop.orders.Count; i++) {
					Console.WriteLine(loop.truck + ";" + loop.day + ";" + (i + 1) + ";" + loop.orders [i].orderID);
				}
			}
		}
		public static Tuple<float, float> CalculateScore (List<Loop> loops) {
			//calculate time
			float time = 0f;
			foreach (Loop loop in loops) {
				time += CalculateLoopDurationAndCapacity(loop).Item1;
			}
			//calculate penalty
			float penalty = 0;
			int declined = 0;
			Dictionary<Order, int> frequencies = GetFrequencies(loops);
			for (int i = 0; i < completeOrderList.Count; i++) {
				if (frequencies [completeOrderList [i]] != 0) {
					declined++;
					penalty += 3f * completeOrderList [i].duration * completeOrderList [i].frequence;
				}
			}
			return new Tuple<float, float>(time, (float)penalty);
		}
		public static List<List<Loop>> GetNeighbours (List<Loop> origin) {
			List<List<Loop>> neighbours = new List<List<Loop>>();
			//remove an order from a loop
			for (int i = 0; i < origin.Count; i++) {
				for (int x = 0; x < origin [i].orders.Count - 1; x++) {
					List<Loop> neighbour = CopyNode(origin);
					bool removedDump = neighbour [i].orders [x].orderID == 0;
					neighbour [i].orders.RemoveAt(x);
					bool skip = false;
					if (removedDump) {
						Tuple<float, List<int>> durationAndCapacity = CalculateLoopDurationAndCapacity(neighbour [i]);
						foreach (int capacity in durationAndCapacity.Item2) {
							if (capacity > 20000) {
								skip = true;
							}
						}
					}
					if (!skip) {
						//remove loop if empty
						if (neighbour [i].orders.Count == 1) {
							neighbour.RemoveAt(i);
						}
						neighbours.Add(neighbour);
					}
				}
			}
			//add an order to a loop
			Dictionary<Order, int> frequencies = GetFrequencies(origin);
			for (int i = 0; i < origin.Count; i++) {
				if (origin [i].orders.Count > 0) {
					Tuple<float, List<int>> timeAndVolume = CalculateLoopDurationAndCapacity(origin [i]);
					float timeLeft = 12f * 60f - timeAndVolume.Item1;
					int innerLoop = 0;
					int capacityLeft = 20000 - timeAndVolume.Item2 [0];
					for (int x = 0; x < origin [i].orders.Count - 1; x++) {
						if (origin [i].orders [x].orderID == 0) {
							innerLoop++;
							capacityLeft = 20000 - timeAndVolume.Item2 [innerLoop];
						} else {
							//find possible orders
							foreach (Order order in completeOrderList) {
								int posA = x == 0 ? dumpID : origin [i].orders [x - 1].matrixID;
								int posB = origin [i].orders [x].matrixID;
								float deltaTime = -GetDistance(posA, posB);
								deltaTime += GetDistance(posA, order.matrixID);
								deltaTime += GetDistance(order.matrixID, posB);
								deltaTime += order.duration;
								if (capacityLeft >= order.containerVolume && timeLeft >= deltaTime && frequencies[order] > 0) {
									//insert order before x
									List<Loop> neighbour = CopyNode(origin);
									neighbour [i].orders.Insert(x, order);
									neighbours.Add(neighbour);
								}
							}
						}
					}
				}
			}
			return neighbours;
		}
		public static List<Loop> CopyNode (List<Loop> origin) {
			List<Loop> result = new List<Loop>();
			foreach (Loop loop in origin) {
				Loop newLoop = new Loop(loop.truck, loop.day);
				foreach (Order order in loop.orders) {
					newLoop.Add(order);
				}
				result.Add(newLoop);
			}
			return result;
		}
		public static Tuple<float, List<int>> CalculateLoopDurationAndCapacity (Loop loop) {
			float time = 0;
			int volume = 0;
			List<int> volumes = new List<int>();
			int pos = dumpID;
			for (int i = 0; i < loop.orders.Count; i++) {
				Order order = loop.orders [i];
				time += GetDistance(pos, order.matrixID);
				pos = order.matrixID;
				if (loop.orders [i].orderID == 0) {
					time += dumpDuration;
					volumes.Add(volume);
					volume = 0;
				} else {
					time += order.duration;
					volume += (int)Math.Round((float)order.containerVolume * 0.2f);
				}
			}
			return new Tuple<float, List<int>>(time, volumes);
		}
		public static Dictionary<Order, int> GetFrequencies (List<Loop> loops) {
			Dictionary<Order, int> frequencies = new Dictionary<Order, int>();
			for (int i = 0; i < completeOrderList.Count; i++) {
				frequencies.Add(completeOrderList [i], completeOrderList [i].frequence);
			}
			foreach (Loop loop in loops) {
				for (int i = 0; i < loop.orders.Count; i++) {
					Order order = loop.orders [i];
					if (order.orderID != 0) {
						frequencies [order]--;
					}
				}
			}
			return frequencies;
		}
	}
}
