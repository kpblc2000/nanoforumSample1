using Teigha.DatabaseServices;
using Teigha.Geometry;

namespace nanoforumSample1.Entities
{
    public class PowerLine
    {
        public string Name { get; set; }
        public List<Point2d> Point { get; set; }
        public ObjectId IDLine { get; set; }
        //public string SigmentLengt { get; set; }
        //public string LengtPowerLine { get; set; }
        public ObjectId Parent { get; set; }
        public string ParentName { get; set; }
        public List<ObjectId> TapsID { get; set; }
        public List<string> TapsName { get; set; }

        public PowerLine()
        {
            Name = null;
            Point = new List<Point2d>();
            IDLine = ObjectId.Null;
            Parent = ObjectId.Null;
            ParentName = null;
            TapsID = new List<ObjectId>();
            TapsName = new List<string>();
        }

    }
}
