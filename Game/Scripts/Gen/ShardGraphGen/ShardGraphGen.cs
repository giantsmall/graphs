using Assets.Game.Scripts.Editors;
using Assets.Game.Scripts.Utility;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace SharpGraph
{
    public class ShardGraphGen : MonoBehaviour
    {
        List<Node> nodeList;
        Graph graph;

        public void GenerateGraph()
        {
            //first create  10 nodes
            var nodes = NodeGenerator.GenerateNodes(10);
            var list = nodes.ToList();
            // this will create a complete graph on 10 nodes, so there are 45 edges.
            graph = GraphGenerator.CreateComplete(nodes);
            graph = GraphGenerator.GenerateRandomGraph(nodes, .5f);
            graph.FindSimplePaths(nodes.First(), nodes.Last());

           
            var cycles = graph.FindSimpleCycles();
        }

        public void OnDrawGizmos()
        {
            //foreach (var edge in graph.GetEdges())
            //{
            //    edge.From().
            //}
        }
    }
}