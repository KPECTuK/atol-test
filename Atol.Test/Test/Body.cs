//#define FROM_FILE
#define FROM_RESOURCE

using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Atol.Test
{
	[StructLayout(LayoutKind.Sequential)]
	public struct Node
	{
		/// <summary>
		///     Индекс корневого узла компонента связности.
		/// </summary>
		public int Parent;
		/// <summary>
		///     Глубина дерева с текущим корневым элементом.
		/// </summary>
		public int Rank;

		public override string ToString()
		{
			return $"{Parent}, {Rank}";
		}
	}

	public struct TestDataSet
	{
		public string Filename;
		public bool Result;
	}

	public class StorageGraph
	{
		private Node[] _set;

		public int Nodes { get; private set; }
		public int Links { get; private set; }

		public StorageGraph()
		{
			_set = new Node[4];
		}

		public void Append(int nodeIndex)
		{
			if(_set.Length <= nodeIndex)
			{
				var @new = new Node[_set.Length * 2];
				unsafe
				{
					fixed(Node* setPtr = _set)
					{
						fixed(Node* newPtr = @new)
						{
							Buffer.MemoryCopy(setPtr, newPtr, @new.Length * sizeof(Node), _set.Length * sizeof(Node));
						}
					}
				}
				_set = @new;
			}

			_set[nodeIndex] = new Node
			{
				Parent = _set[nodeIndex].Parent == 0 ? nodeIndex : _set[nodeIndex].Parent,
				Rank = _set[nodeIndex].Rank,
			};

			// предополагается что нумерация узлов непрерывна
			nodeIndex += 1;
			Nodes = Nodes < nodeIndex ? nodeIndex : Nodes;
		}

		//public int FindRootIndex(int nodeIndex)
		//{
		//	if(nodeIndex == _set[nodeIndex].Parent)
		//	{
		//		return nodeIndex;
		//	}
		//	return _set[nodeIndex].Parent = FindRootIndex(_set[nodeIndex].Parent);
		//}		

		public int FindRootIndex(int nodeIndex)
		{
			// рекурсия потребляет больше памяти при фактическом сохранении
			// второго прохода в процессе раскрутки стека

			var current = nodeIndex;
			while (_set[current].Parent != current)
			{
				current = _set[current].Parent;
			}
			while (_set[nodeIndex].Parent != current)
			{
				var temp = _set[nodeIndex].Parent;
				_set[nodeIndex].Parent = current;
				nodeIndex = temp;
			}

			return current;
		}

		public void Union(int leftNodeIndex, int rightNodeIndex)
		{
			leftNodeIndex = FindRootIndex(leftNodeIndex);
			rightNodeIndex = FindRootIndex(rightNodeIndex);

			if(_set[leftNodeIndex].Rank < _set[rightNodeIndex].Rank)
			{
				leftNodeIndex ^= rightNodeIndex;
				rightNodeIndex ^= leftNodeIndex;
				leftNodeIndex ^= rightNodeIndex;
			}

			_set[rightNodeIndex].Parent = leftNodeIndex;

			if(_set[leftNodeIndex].Rank == _set[rightNodeIndex].Rank)
			{
				++_set[leftNodeIndex].Rank;
			}

			Links++;
		}

		public bool IsGaps()
		{
			var index = 0;
			var value = _set[index].Parent;
			for(; index < Nodes && value == _set[index].Parent; index++) { }
			return index < Nodes;
		}
	}

	public class DataSource
	{
#if FROM_FILE
		private static readonly string _filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "atol-test-data-source");
#endif
		public static readonly StorageGraph Error = new StorageGraph();

		private static readonly Regex _match = new Regex("Node(?'left'[0-9]+);Node(?'right'[0-9]+)");
		private static readonly TestDataSet[] _testSets =
		{
			new TestDataSet { Filename = "test-00", Result = true },
			new TestDataSet { Filename = "test-01", Result = false },
			//new TestDataSet { Filename = "test-02", Result = true },
		};

		public static StorageGraph Build(StreamReader reader)
		{
			var result = new StorageGraph();
			while(!reader.EndOfStream)
			{
				var match = _match.Match(reader.ReadLine() ?? string.Empty);
				if(!match.Success)
				{
					continue;
				}

				var left = int.Parse(match.Groups["left"].Value);
				var right = int.Parse(match.Groups["right"].Value);
				result.Append(left);
				result.Append(right);
				result.Union(left, right);
			}

			return result;
		}

#if FROM_FILE
		public static IEnumerable A_FileDataSource => _testSets.Select(_ => new TestCaseData(BuildFromFile(_.Filename)).SetDescription(_.Filename).Returns(_.Result));

		private static StorageGraph BuildFromFile(string filename)
		{
			try
			{
				using(var stream = new FileStream(new FileInfo(Path.Combine(_filePath, $"{filename}.csv")).FullName, FileMode.Open))
				{
					using(var reader = new StreamReader(stream))
					{
						return Build(reader);
					}
				}
			}
			catch
			{
				return Error;
			}
		}
#endif
#if FROM_RESOURCE
		public static IEnumerable A_ResourcesDataSource => _testSets.Select(_ => new TestCaseData(BuildFromResource(_.Filename)).SetDescription(_.Filename).Returns(_.Result));

		private static StorageGraph BuildFromResource(string filename)
		{
			try
			{
				using(var stream = Assembly
					.GetExecutingAssembly()
					.GetManifestResourceStream($"Atol.Resources.{filename}.csv"))
				{
					using(var reader = new StreamReader(stream))
					{
						return Build(reader);
					}
				}
			}
			catch
			{
				return Error;
			}
		}
#endif
	}

	[TestFixture]
	public class Body
	{
#if FROM_RESOURCE
		[TestCaseSource(typeof(DataSource), nameof(DataSource.A_ResourcesDataSource))]
#endif
#if FROM_FILE
		[TestCaseSource(typeof(DataSource), nameof(DataSource.A_FileDataSource))]
#endif
		public static bool A_Check(StorageGraph graph)
		{
			if(ReferenceEquals(graph, DataSource.Error))
			{
				throw new Exception("input data not found");
			}

			//var componentsMax = graph.Nodes - (Math.Sqrt(8d * graph.Links + 1d) - 1d) * .5;
			var isGap = graph.IsGaps();
			//return componentsMax == 1d || !isGap;
			return !isGap;
		}
	}
}