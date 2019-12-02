using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Garbage {
	class Program {
		public static int [,] distances;
		static void Main (string [] args) {
			List<Order> orders = new List<Order>();

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
					orders.Add(new Order(orderID, frequence, containerVolume, duration, matrixID)); //add order to list
					Console.Write(parameters [7]);
					Console.Write(" ");
					Console.WriteLine(parameters [8]);
				}
			}

			distances = new int [maxMatrixID, maxMatrixID];
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

			Console.Read();
		}
	}
}
