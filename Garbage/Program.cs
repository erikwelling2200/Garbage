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

		static void Main (string [] args) {
			List<Order> orders = new List<Order>();
			Node node = new Node();

			//read order file
			bool decimalComma = float.Parse("12.5") == 125f;
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
					string durationString = decimalComma ? parameters [5].Replace('.', ',') : parameters [5];
					float duration = float.Parse(durationString);
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
						SubLoop newSubLoop = new SubLoop();
						currentOrders.AddRange(todayOrders);
						float heuristicSumTravel = 0, heuristicSumEmpty = 0, loopduration = 0, loopCapacity = 0;
						int loopPos = currentLocation;
						while (currentOrders.Count > 0) {
							float bestHeuristic = float.NegativeInfinity, bestHeuristicTravel = float.NegativeInfinity, bestHeuristicEmpty = float.NegativeInfinity;
							Order bestOrder = currentOrders [0];
							for (int i = currentOrders.Count - 1; i >= 0; i--) {
								Order order = currentOrders [i];
								float newHeuristicTravel, newHeuristicEmpty;
								bool enoughCapacity = currentCapacity >= loopCapacity + order.containerVolume * 0.2f;
								bool enoughTime = timeLeft >= loopduration + GetDistance(loopPos, order.matrixID) + order.duration + GetDistance(order.matrixID, dumpID) + dumpDuration;
								if (enoughCapacity && enoughTime) {
									newHeuristicTravel = GetDistance(loopPos, order.matrixID);
									newHeuristicEmpty = order.duration;
									float newHeuristic = 2f * newHeuristicEmpty - newHeuristicTravel;
									if (newHeuristic > bestHeuristic) {
										bestHeuristic = newHeuristic;
										bestHeuristicEmpty = newHeuristicEmpty;
										bestHeuristicTravel = newHeuristicTravel;
										bestOrder = order;
									}
								} else {
									currentOrders.Remove(order);
								}
							}
							if (currentOrders.Count > 0) {
								heuristicSumEmpty += bestHeuristicEmpty;
								heuristicSumTravel += bestHeuristicTravel;
								newSubLoop.orders.Add(bestOrder);
								loopduration += GetDistance(loopPos, bestOrder.matrixID);
								loopduration += bestOrder.duration;
								loopPos = bestOrder.matrixID;
								loopCapacity += bestOrder.containerVolume * 0.2f;
								currentOrders.Remove(bestOrder);
							}
						}
						heuristicSumTravel += GetDistance(loopPos, dumpID);
						heuristicSumEmpty += dumpDuration;

						//sort the new loop
						if (newSubLoop.orders.Count > 0) {
							Order dump = new Order();
							newSubLoop.orders.Add(dump);
							Tuple<SubLoop, float> SM = SolveTravellingSM(newSubLoop);
							if (SM.Item2 < heuristicSumTravel) {
								heuristicSumTravel = SM.Item2;
								newSubLoop = SM.Item1;
							}
							int dumpIndex = newSubLoop.orders.IndexOf(dump);
							newSubLoop = OffsetSubLoop(newSubLoop, -(dumpIndex+1));
						}

						//Console.WriteLine("SUM: " + heuristicSum);
						if ((2f * heuristicSumEmpty - heuristicSumTravel) > 0 && newSubLoop.orders.Count > 0) {
							for (int i = 0; i < newSubLoop.orders.Count; i++) {
								Order order = newSubLoop.orders [i];
								//collect garbage
								timeLeft -= GetDistance(currentLocation, order.matrixID); //move to order
								currentLocation = order.matrixID;
								timeLeft -= order.duration; //collect garbage
								currentCapacity -= order.containerVolume * 0.2f; //lose capacity
								todayOrders.Remove(order);
								orders.Remove(order);
							}
							node.loops [truck - 1, day - 1].subLoops.Add(newSubLoop);

							//empty
							timeLeft -= GetDistance(currentLocation, dumpID); //move to dump
							currentLocation = dumpID; //set location to dump
							timeLeft -= dumpDuration; //empty
							currentCapacity = 20000; //reset capacity

							//dayLoop.AddDump();
						} else {
							todayOrders.Clear();
						}
					}
				}
			}
			/*Node test = new Node();
			SubLoop testSubLoop = new SubLoop();
			testSubLoop.orders.Add(completeOrderList[5]);
			testSubLoop.orders.Add(new Order());
			test.loops [0, 0].subLoops.Add(testSubLoop);

			SubLoop testSubLoop2 = new SubLoop();
			testSubLoop2.orders.Add(completeOrderList [5]);
			testSubLoop2.orders.Add(new Order());
			test.loops [0, 3].subLoops.Add(testSubLoop2);
			PrintResult(test);
			Console.WriteLine(test.GetScore());
			Neighbour testNeighbour = new Neighbour(test, null, 1, 1, 0);

			Console.WriteLine(test.GetScore());

			Console.WriteLine(testNeighbour.GetScore());
			Node testNode = testNeighbour.ToNode();
			Console.WriteLine(CalculateScore(testNode));

			//PrintResult(testNode);

			/*SubLoop testNewSubLoop = new SubLoop();
			testNewSubLoop.orders.AddRange(testSubLoop.orders);
			testNewSubLoop.orders.Insert(1, completeOrderList[65]);
			testNewSubLoop.orders.Insert(1, new Order());
			Neighbour testNeighbour2 = new Neighbour(test, testNewSubLoop, 1, 1, 0);
			Console.WriteLine(test.GetScore());
			Console.WriteLine(testNeighbour.GetScore());
			Console.WriteLine(testNeighbour2.GetScore());
			Dictionary<int, List<int>> pds = testNeighbour.CalculatePickupDays();
			Node node2 = testNeighbour2.ToNode();
			PrintResult(node2);
			Console.WriteLine(node2.GetScore());
			Console.WriteLine(testNewSubLoop.GetTimeAndVolume());*/


			Tuple<float, float> bestScore = node.GetScore();
			int a = 1;
			DateTime start = DateTime.Now;
			int totalNeighbourCount = 0;

			//settings
			float T = 5;
			int stepsPerT = 16*50;
			float Tfactor = 0.95f;
			float TTreshold = 0.05f;

			int stepsTillThreshold = (int)Math.Ceiling(Math.Log(TTreshold / T, Tfactor));
			while (T > TTreshold) {
				for (int i = 0; i < stepsPerT; i++) {
					if (a%10 == 0) {
						Console.WriteLine(a + " / " + stepsPerT * stepsTillThreshold + ": " + (bestScore.Item1 + bestScore.Item2));
						Console.WriteLine("extrapolated time remaining: " + TimeSpan.FromSeconds((int)Math.Round((DateTime.Now.Subtract(start).TotalSeconds / (float)a) * (float)(stepsPerT * stepsTillThreshold - a))));
					}
					a++;
					DateTime startTime = DateTime.Now;
					List<Neighbour> neighbours = GetNeighbours(node);
					totalNeighbourCount += neighbours.Count;
					//Console.WriteLine("neighbourDuration: " + (DateTime.Now.Subtract(startTime)));
					//Console.WriteLine("neighbours:        " + neighbours.Count);
					bool accepted = false;
					Neighbour neighbour = null;
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
							double r = random.NextDouble();
							if (r < p) {
								accepted = true;
							}
						}
						x++;
					}
					//Console.WriteLine("acceptDuration:    " + (DateTime.Now.Subtract(startTime)) + " in " +x+" tries");
					node = neighbour.ToNode();
					bestScore = score;
				}
				T *= Tfactor;
			}
			//remove unfeasable single pickups
			Dictionary<int, List<int>> pickupDays = node.GetPickupDays();
			for (int i = 0; i < completeOrderList.Count; i++) {
				bool accept = false;
				List<int> days = pickupDays [completeOrderList [i].orderID];
				switch (completeOrderList [i].frequence) {
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
					for (int truck = 1; truck <= 2; truck++) {
						foreach (int day in days) {
							for (int x = 0; x < node.loops [truck - 1, day - 1].subLoops.Count; x++) {
								node.loops [truck - 1, day - 1].subLoops [x].orders.Remove(completeOrderList [i]);
							}
						}
					}
				}
			}
			bestScore = CalculateScore(node);

			PrintResult(node);
			Console.WriteLine("search duration: " + DateTime.Now.Subtract(start));
			Console.WriteLine("average neighbour count: " + (totalNeighbourCount / a));
			Console.WriteLine("Traveling time: " + bestScore.Item1);
			Console.WriteLine("Decline penalty: " + bestScore.Item2);
			Console.WriteLine("Total score: " + (bestScore.Item1 + bestScore.Item2));
			Console.Read();
		}
		public static SubLoop OffsetSubLoop (SubLoop subLoop, int offset) {
			List<Order> newOrders = new List<Order>();
			for (int i = 0; i < subLoop.orders.Count; i++) {
				int newI = (i - offset + subLoop.orders.Count) % subLoop.orders.Count;
				newOrders.Add(subLoop.orders [newI]);
			}
			subLoop.orders = newOrders;
			return subLoop;
		}
		public static Tuple<SubLoop, float> SolveTravellingSM (SubLoop origin) {
			List<Order> bestList = null;
			float smallestDistance = float.MaxValue;
			for (int i = 0; i < origin.orders.Count; i++) {
				List<Order> orders = new List<Order>();
				orders.AddRange(origin.orders);
				List<Order> newList = new List<Order>();
				Order order = orders [i];
				newList.Add(order);
				orders.RemoveAt(i);
				float distance = 0;
				while (orders.Count > 0) {
					Tuple<Order, float> closestOrder = FindClosestOrder(order, orders);
					Order nextOrder = closestOrder.Item1;
					newList.Add(nextOrder);
					orders.Remove(nextOrder);
					order = nextOrder;
					distance += closestOrder.Item2;
				}
				//close the loop
				distance += GetDistance(newList [0], newList [newList.Count - 1]);
				if (distance < smallestDistance) {
					//Console.WriteLine(distance);
					smallestDistance = distance;
					bestList = newList;
				}
			}
			//Console.WriteLine("done");
			SubLoop subLoop = new SubLoop();
			subLoop.orders = bestList;
			return new Tuple<SubLoop, float>(subLoop, smallestDistance);
		}
		public static Tuple<Order, float> FindClosestOrder (Order a, List<Order> bs) {
			float smallestDistance = float.MaxValue;
			Order closestOrder = null;
			foreach (Order b in bs) {
				if (a != b) {
					float distance = GetDistance(a, b);
					if (distance < smallestDistance) {
						smallestDistance = distance;
						closestOrder = b;
					}
				}
			}
			return new Tuple<Order, float> (closestOrder, smallestDistance);
		}
		public static float GetDistance (Order a, Order b) {
			return GetDistance(a.matrixID, b.matrixID);
		}
		public static float GetDistance (int matrixID1, int matrixID2) {
			return (float)distances [matrixID1, matrixID2] / 60f;
		}
		public static void PrintResult (Node node) {
			foreach (Loop loop in node.loops) {
				int i = 1;
				foreach (SubLoop subLoop in loop.subLoops) {
					foreach (Order order in subLoop.orders) {
						Console.WriteLine(loop.truck + ";" + loop.day + ";" + i + ";" + order.orderID);
						i++;
					}
				}
			}
		}
		public static Tuple<float, float> CalculateScore (Neighbour neighbour) {
			//calculate time
			float time = 0f;
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					Loop loop = neighbour.origin.loops [truck - 1, day - 1];
					for (int i = 0; i <= loop.subLoops.Count; i++) {
						SubLoop subLoop = null;
						if (neighbour.truck == truck && neighbour.day == day && neighbour.subLoopIndex == i) {
							subLoop = neighbour.newSubLoop;
						} else if (i < loop.subLoops.Count) {
							subLoop = loop.subLoops [i];
						}
						if (subLoop != null) {
							time += subLoop.GetTimeAndVolume().Item1;
						}
					}
				}
			}
			//calculate penalty
			float penalty = 0;
			int declined = 0;
			Dictionary<int, List<int>> pickupDays = neighbour.GetPickupDays();
			for (int i = 0; i < completeOrderList.Count; i++) {
				bool accept = false;
				List<int> days = pickupDays [completeOrderList [i].orderID];
				switch (completeOrderList [i].frequence) {
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
					penalty += 3f * completeOrderList [i].duration * completeOrderList [i].frequence;
				}
			}
			//Console.WriteLine(declined);
			return new Tuple<float, float>(time, (float)penalty);
		}
		public static Tuple<float, float> CalculateScore (Node node) {
			//calculate time
			float time = 0f;
			foreach (Loop loop in node.loops) {
				time += loop.GetTimeAndVolume().Item1;
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
			//Console.WriteLine(declined);
			return new Tuple<float, float>(time, (float)penalty);
		}
		public static List<Neighbour> GetNeighbours (Node origin) {
			Dictionary<int, List<int>> frequencies = GetPickupDays(origin);
			List<Order> possibleOrders = new List<Order>();
			foreach (Order order in completeOrderList) {
				if (frequencies[order.orderID].Count < order.frequence) {
					possibleOrders.Add(order);
				}
			}
			possibleOrders.Add(new Order());

			List<Neighbour> neighbours = new List<Neighbour>();
			DateTime time = DateTime.Now;
			//remove an order from a loop
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					for (int i = 0; i < origin.loops[truck-1, day-1].subLoops.Count; i++) {
						for (int x = 0; x < origin.loops [truck-1, day-1].subLoops[i].orders.Count - 1; x++) {
							SubLoop newSubLoop = new SubLoop();
							newSubLoop.orders.AddRange(origin.loops [truck-1, day-1].subLoops [i].orders);
							newSubLoop.orders.RemoveAt (x);
							if (newSubLoop.orders.Count == 1) {
								newSubLoop = null;
							}
							Neighbour neighbour = new Neighbour(origin, newSubLoop, truck, day, i);
							neighbours.Add(neighbour);
						}
					}
				}
			}
			//Console.WriteLine(neighbours.Count);
			//Console.WriteLine("removeDuration:    " + DateTime.Now.Subtract(time));
			time = DateTime.Now;
			//add an order to a loop
			List<Thread> [,] threads = new List<Thread> [2,5];
			List<List<SubLoop>>[,] threadResults = new List<List<SubLoop>>[2,5];
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					Tuple<float, List<int>> timeAndDistance = origin.loops [truck-1, day-1].GetTimeAndVolume();
					List<List<SubLoop>> loopResults = new List<List<SubLoop>>();
					threadResults [truck - 1, day - 1] = loopResults;
					threads [truck - 1, day - 1] = new List<Thread>();
					for (int i = 0; i < origin.loops[truck-1, day-1].subLoops.Count; i++) {
						SubLoop subLoop = origin.loops [truck-1, day-1].subLoops [i];
						List<SubLoop> threadResult = new List<SubLoop>();
						loopResults.Add(threadResult);
						int distance = timeAndDistance.Item2 [i];
						Thread thread = new Thread(() => AddOrdersToLoop(subLoop, timeAndDistance.Item1, distance, possibleOrders, threadResult));
						threads [truck - 1, day - 1].Add(thread);
						thread.Start();
					}
				}
			}
			//add a new subloop
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					Tuple<float, List<int>> timeAndDistance = origin.loops [truck - 1, day - 1].GetTimeAndVolume();
					float timeLeft = 12f * 60f - timeAndDistance.Item1;
					if (timeLeft >= dumpDuration) {
						foreach (Order order in possibleOrders) {
							if (order.orderID != 0 && timeLeft >= dumpDuration + GetDistance(dumpID, order.matrixID) + GetDistance(order.matrixID, dumpID) + order.duration) {
								SubLoop subLoop = new SubLoop();
								subLoop.orders.Add(order);
								subLoop.orders.Add(new Order());
								Neighbour neighbour = new Neighbour(origin, subLoop, truck, day, origin.loops [truck - 1, day - 1].subLoops.Count);
								neighbours.Add(neighbour);
							}
						}
					}
				}
			}
			//Console.WriteLine(neighbours.Count);
			//untangle a loop
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					for (int i = 0; i < origin.loops[truck-1, day-1].subLoops.Count; i++) {
						SubLoop subLoop = origin.loops [truck - 1, day - 1].subLoops [i].GetUntangledVersion();
						Neighbour neighbour = new Neighbour(origin, subLoop, truck, day, i);
						neighbours.Add(neighbour);
					}
				}
			}
			//Console.WriteLine(neighbours.Count);
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					List<List<SubLoop>> loopResult = threadResults [truck - 1, day - 1];
					for (int i = 0; i < threads[truck-1, day-1].Count; i++) {
						threads [truck - 1, day - 1] [i].Join();
						List<SubLoop> threadResult = loopResult [i];
						if (threadResult != null) {
							for (int x = 0; x < threadResult.Count; x++) {
								Neighbour neighbour = new Neighbour(origin, threadResult [x], truck, day, i);
								neighbours.Add(neighbour);
							}
						}
					}
				}
			}
			//Console.WriteLine(neighbours.Count);
			return neighbours;
		}
		public static void AddOrdersToLoop(SubLoop subLoop, float subTime, int subDistance, List<Order> possibleOrders, List<SubLoop> result) {
			float timeLeft = 12f * 60f - subTime;
			int capacityLeft = 20000 - subDistance;
			//find possible orders
			foreach (Order order in possibleOrders) {
				if (!subLoop.orders.Contains(order)) {
					for (int x = 0; x < subLoop.orders.Count; x++) {
						int posA = x == 0 ? dumpID : subLoop.orders[x - 1].matrixID;
						int posB = subLoop.orders[x].orderID == 0 ? dumpID : subLoop.orders[x].matrixID;
						int posC = order.orderID == 0 ? dumpID : order.matrixID;
						float deltaTime = -GetDistance(posA, posB);
						deltaTime += GetDistance(posA, posC);
						deltaTime += GetDistance(posC, posB);
						deltaTime += order.duration;
						if (timeLeft >= deltaTime && ((order.orderID == 0 && x > 0 && x < subLoop.orders.Count - 1) || (capacityLeft >= (float)(order.containerVolume) * 0.2f))) {
							//insert order before x
							SubLoop newSubLoop = new SubLoop();
							newSubLoop.orders.AddRange(subLoop.orders);
							newSubLoop.orders.Insert(x, order);
							result.Add(newSubLoop);
						}
					}
				}
			}
		}

		public static Tuple<float, int> CalculateLoopDurationAndCapacity (SubLoop subLoop) {
			float time = 0;
			int volume = 0;
			int pos = dumpID;
			for (int i = 0; i < subLoop.orders.Count; i++) {
				Order order = subLoop.orders [i];
				time += GetDistance(pos, order.matrixID);
				if (order.orderID == 0) {
					time += dumpDuration;
				}
				pos = order.matrixID;
				time += order.duration;
				volume += (int)Math.Round((float)order.containerVolume * 0.2f);
			}
			return new Tuple<float, int>(time, volume);
		}
		public static Dictionary<int, List<int>> GetPickupDays(Node node) {
			Dictionary<int, List<int>> pickupDays = new Dictionary<int, List<int>>();
			for (int i = 0; i < completeOrderList.Count; i++) {
				pickupDays.Add(completeOrderList[i].orderID, new List<int>());
			}
			for (int truck = 1; truck <= 2; truck++) {
				for (int day = 1; day <= 5; day++) {
					for (int i = 0; i < node.loops[truck-1, day-1].subLoops.Count; i++) {
						for (int x = 0; x < node.loops [truck - 1, day - 1].subLoops [i].orders.Count - 1; x++) {
							Order order = node.loops [truck - 1, day - 1].subLoops [i].orders [x];
							if (order.orderID != 0) {
								pickupDays [order.orderID].Add(day);
							}
						}
					}
				}
			}
			return pickupDays;
		}
	}
}
