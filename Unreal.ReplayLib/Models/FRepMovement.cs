namespace Unreal.ReplayLib.Models
{
    public class FRepMovement
    {
        public FVector? Location { get; set; }
        public FRotator? Rotation { get; set; }
        public bool BSimulatedPhysicSleep { get; set; }
        public bool BRepPhysics { get; set; }
        public override string ToString() => Location.ToString();
    }
}