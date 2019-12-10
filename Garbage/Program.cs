using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace Garbage {
	class Program {
		public static int [,] distances;
		public static int dumpID = 287, dumpDuration = 30;
		public static List<Order> completeOrderList = new List<Order>();
		public static Random random = new Random();
		public static Program test;
		public static Thread thread;

		static void Main (string [] args) {
			List<Order> orders = new List<Order>();
			Node node = new Node(new List<Loop>());
			test = new Program();

			//read order file
			int maxMatrixID = 0;
			StreamReader orderFile = new StreamReader("Orderbestand.txt");
			string line = orderFile.ReadLine();
			while (line != null) {
				line = orderFile.ReadLine();
				if (line != null) {
					string[] parameters = line.Split(';');
					int orderID = int.Parse(parameters[0]);
					int frequence = int.Parse(parameters[2].Substring(0, 1));
					int containerVolume = int.Parse(parameters[3]) * int.Parse(parameters[4]);
					float duration = float.Parse(parameters[5].Replace('.', ','));
					int matrixID = int.Parse(parameters[6]);
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
					node.loops.Add(pickupLoop);
				}
			}
			Tuple<float, float> bestScore = CalculateScore(node);

			float T = 30;
			int a = 1;
			int totalNeighbourCount = 0;
			DateTime start = DateTime.Now;
			while (T > 0.15f) {
				for (int i = 0; i < (100*16); i++) {
					if (a%100 == 0) {
						Console.WriteLine(a + " / " + ((100*16) * 104) + ": " + (bestScore.Item1 + bestScore.Item2));
						Console.WriteLine("extrapolated time remaining: " + TimeSpan.FromSeconds((int)Math.Round(DateTime.Now.Subtract(start).TotalSeconds / (float)a * (float)(100 * 16 * 104 - a))));
					}
					a++;
					DateTime startTime = DateTime.Now;
					List<Node> neighbours = GetNeighbours(node);
					totalNeighbourCount += neighbours.Count;
					//Console.WriteLine("neighbourDuration: " + (DateTime.Now.Subtract(startTime)));
					//Console.WriteLine("neighbours:        " + neighbours.Count);
					bool accepted = false;
					Node neighbour = null;
					Tuple<float, float> score = null;
					int x = 0;
					startTime = DateTime.Now;
					while (accepted == false) {
						neighbour = neighbours [random.Next(neighbours.Count)];
						score = neighbour.GetScore();
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
						x++;
					}
					//Console.WriteLine("acceptDuration:    " + (DateTime.Now.Subtract(startTime)) + " in " +x+" tries");
					node = neighbour;
					bestScore = score;
				}
				T *= 0.95f;
			}
			PrintResult(node);
			Console.WriteLine("search duration: " + DateTime.Now.Subtract(start));
			Console.WriteLine("average neighbour count: " + (totalNeighbourCount / a));
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
		public static void PrintResult (Node node) {
			foreach (Loop loop in node.loops) {
				for (int i = 0; i < loop.orders.Count; i++) {
					Console.WriteLine(loop.truck + ";" + loop.day + ";" + (i + 1) + ";" + loop.orders [i].orderID);
				}
			}
		}
		public static Tuple<float, float> CalculateScore (Node node) {
			//calculate time
			float time = 0f;
			foreach (Loop loop in node.loops) {
				time += CalculateLoopDurationAndCapacity(loop).Item1;
			}
			//calculate penalty
			float penalty = 0;
			int declined = 0;
			Dictionary<int, List<int>> pickupDays = GetPickupDays(node);
			for (int i = 0; i < completeOrderList.Count; i++) {
				bool accept = false;
				List<int> days = pickupDays[completeOrderList[i].orderID];
				switch (completeOrderList[i].frequence) {
					case 1:
						if (days.Count > 0) {
							accept = true;
						}
						break;
					case 2:
						if ((days.Contains(1) && days.Contains(4)) || (days.Contains(2) && days.Contains(5))) {
							accept = true;
						}
						break;
					case 3:
						if (days.Contains(1) && days.Contains(3) && days.Contains(5)) {
							accept = true;
						}
						break;
					case 4:
						if ((days.Contains(1) && days.Contains(2) && days.Contains(3) && days.Contains(4)) || 
							(days.Contains(1) && days.Contains(2) && days.Contains(3) && days.Contains(5)) || 
							(days.Contains(1) && days.Contains(2) && days.Contains(4) && days.Contains(5)) ||
							(days.Contains(1) && days.Contains(3) && days.Contains(4) && days.Contains(5)) ||
							(days.Contains(2) && days.Contains(3) && days.Contains(4) && days.Contains(5))) {
							accept = true;
						}
						break;
				}
				if (!accept) {
					declined++;
					penalty += 3f * completeOrderList[i].duration * completeOrderList[i].frequence;
				}
			}
			return new Tuple<float, float>(time, (float)penalty);
		}
		public static List<Node> GetNeighbours (Node origin) {
			List<Node> neighbours = new List<Node>();
			DateTime time = DateTime.Now;
			//remove an order from a loop
			for (int i = 0; i < origin.loops.Count; i++) {
				for (int x = 0; x < origin.loops [i].orders.Count - 1; x++) {
					Node neighbour = CopyNode(origin);
					bool removedDump = neighbour.loops [i].orders [x].orderID == 0;
					neighbour.loops [i].orders.RemoveAt(x);
					bool skip = false;
					if (removedDump) {
						Tuple<float, List<int>> durationAndCapacity = CalculateLoopDurationAndCapacity(neighbour.loops [i]);
						foreach (int capacity in durationAndCapacity.Item2) {
							if (capacity > 20000) {
								skip = true;
							}
						}
					}
					if (!skip) {
						//remove loop if empty
						if (neighbour.loops [i].orders.Count == 1) {
							neighbour.loops.RemoveAt(i);
						}
						neighbours.Add(neighbour);
					}
				}
			}
			//Console.WriteLine("removeDuration:    " + DateTime.Now.Subtract(time));
			time = DateTime.Now;
			//add an order to a loop
			Dictionary<int, int> frequencies = GetFrequencies(origin);
			List<Thread> threads = new List<Thread>();
			List<List<Node>> threadResults = new List<List<Node>>();
			for (int i = 0; i < origin.loops.Count; i++) {
				Loop loop = origin.loops[i];				
				if (loop.orders.Count > 0) {
					List<Node> threadResult = new List<Node>();
					threadResults.Add(threadResult);
					int k = i;
					Thread thread = new Thread(() => AddOrdersToLoop(loop, k, origin, frequencies, threadResult));
					threads.Add(thread);
					thread.Start();
					//AddOrdersToLoop(loop, k, origin, frequencies, threadResult);
				}
			}
			for (int i = 0; i < threadResults.Count; i++) {
				threads[i].Join();
				neighbours.AddRange(threadResults[i]);
			}
			return neighbours;
		}
		public static void AddOrdersToLoop(Loop loop, int i, Node origin, Dictionary<int, int> frequencies, List<Node> result) {
			Tuple<float, List<int>> timeAndVolume = CalculateLoopDurationAndCapacity(loop);
			float timeLeft = 12f * 60f - timeAndVolume.Item1;
			int innerLoop = 0;
			int capacityLeft = 20000 - timeAndVolume.Item2[0];
			for (int x = 0; x < loop.orders.Count - 1; x++) {
				if (loop.orders[x].orderID == 0) {
					innerLoop++;
					capacityLeft = 20000 - timeAndVolume.Item2[innerLoop];
				} else {
					//find possible orders
					foreach (Order order in completeOrderList) {
						int posA = x == 0 ? dumpID : loop.orders[x - 1].matrixID;
						int posB = loop.orders[x].matrixID;
						float deltaTime = -GetDistance(posA, posB);
						deltaTime += GetDistance(posA, order.matrixID);
						deltaTime += GetDistance(order.matrixID, posB);
						deltaTime += order.duration;
						if (capacityLeft >= order.containerVolume && timeLeft >= deltaTime && frequencies[order.orderID] > 0) {
							//insert order before x
							Node neighbour = CopyNode(origin);
							neighbour.loops[i].orders.Insert(x, order);
							result.Add(neighbour);
						}
					}
				}
			}
		}
		public static Node CopyNode (Node origin) {
			List<Loop> newLoops = new List<Loop>();
			foreach (Loop loop in origin.loops) {
				Loop newLoop = new Loop(loop.truck, loop.day);
				foreach (Order order in loop.orders) {
					newLoop.Add(order);
				}
				newLoops.Add(newLoop);
			}
			return new Node(newLoops);
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
		public static Dictionary<int, List<int>> GetPickupDays(Node node) {
			List<Loop> loops = node.loops;
			Dictionary<int, List<int>> pickupDays = new Dictionary<int, List<int>>();
			for (int i = 0; i < completeOrderList.Count; i++) {
				pickupDays.Add(completeOrderList[i].orderID, new List<int>());
			}
			foreach (Loop loop in loops) {
				for (int i = 0; i < loop.orders.Count; i++) {
					Order order = loop.orders[i];
					if (order.orderID != 0) {
						pickupDays[order.orderID].Add(loop.day);
					}
				}
			}
			return pickupDays;
		}
		public static Dictionary<int, int> GetFrequencies (Node node) {
			List<Loop> loops = node.loops;
			Dictionary<int, int> frequencies = new Dictionary<int, int>();
			for (int i = 0; i < completeOrderList.Count; i++) {
				frequencies.Add(completeOrderList [i].orderID, completeOrderList [i].frequence);
			}
			foreach (Loop loop in loops) {
				for (int i = 0; i < loop.orders.Count; i++) {
					Order order = loop.orders [i];
					if (order.orderID != 0) {
						frequencies [order.orderID]--;
					}
				}
			}
			return frequencies;
		}
	}
}
