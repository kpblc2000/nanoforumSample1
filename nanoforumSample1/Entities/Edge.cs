using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace nanoforumSample1.Entities
{
    public class Edge
    {
        public string Name { get; set; }
        public Point3d StartPoint { get; set; }
        public Point3d CentrPoint { get; set; }
        public Point3d EndPoint { get; set; }

        public ObjectId IDLine { get; set; }



        public Edge()
        {
            Name = null;
            IDLine = ObjectId.Null;
            StartPoint = new Point3d();
            EndPoint = new Point3d();
            CentrPoint = new Point3d();

        }

    }
}
