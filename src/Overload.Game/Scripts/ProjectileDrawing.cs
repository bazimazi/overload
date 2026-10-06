using Godot;

namespace Overload.Game;

public partial class CombatEffects
{
    private MultiMesh? projectileBodies,projectileCores,projectileTails;
    private ArrayMesh? projectileCircle;
    private QuadMesh? projectileQuad;
    private readonly float[] bodyInstances=new float[200*12],coreInstances=new float[200*12],tailInstances=new float[200*12];
    private void PrepareProjectileDrawing()
    {
        if(projectileBodies is not null)return;
        using var builder=new SurfaceTool();builder.Begin(Mesh.PrimitiveType.Triangles);builder.SetColor(Colors.White);
        for(var i=0;i<32;i++)
        {
            var a=Vector2.FromAngle(Mathf.Tau*i/32);var b=Vector2.FromAngle(Mathf.Tau*(i+1)/32);
            builder.AddVertex(Vector3.Zero);builder.AddVertex(new(a.X,a.Y,0));builder.AddVertex(new(b.X,b.Y,0));
        }
        projectileCircle=builder.Commit();projectileQuad=new QuadMesh { Size=new(2,2) };
        MultiMesh Instances(Mesh mesh)=>new() { TransformFormat=MultiMesh.TransformFormatEnum.Transform2D,UseColors=true,InstanceCount=200,Mesh=mesh };
        projectileBodies=Instances(projectileCircle);projectileCores=Instances(projectileCircle);projectileTails=Instances(projectileQuad);
        Instance(bodyInstances,0,new(2,3),new(4,5),new(6,7),Colors.White);projectileBodies.Buffer=bodyInstances;
        var check=projectileBodies.GetInstanceTransform2D(0);
        if(check.X!=new Vector2(2,3)||check.Y!=new Vector2(4,5)||check.Origin!=new Vector2(6,7))
            throw new InvalidOperationException("Unexpected Godot 2D instance buffer layout");
    }
    private static void Instance(float[] data,int index,Vector2 x,Vector2 y,Vector2 origin,Color color)
    {
        var offset=index*12;
        // Godot's 2D instance buffer stores two padded matrix rows, followed by RGBA.
        data[offset]=x.X;data[offset+1]=y.X;data[offset+2]=0;data[offset+3]=origin.X;
        data[offset+4]=x.Y;data[offset+5]=y.Y;data[offset+6]=0;data[offset+7]=origin.Y;
        data[offset+8]=color.R;data[offset+9]=color.G;data[offset+10]=color.B;data[offset+11]=color.A;
    }
    private void DrawProjectiles()
    {
        // The dummy headless renderer does not store MultiMesh buffers or transforms.
        if(DisplayServer.GetName()=="headless")return;
        PrepareProjectileDrawing();
        for(var i=0;i<projectiles.Count;i++)
        {
            var p=projectiles[i];var color=p.Friendly?new Color("a0f7e5"):new Color("ffd397");
            Instance(bodyInstances,i,Vector2.Right*p.Radius,Vector2.Down*p.Radius,p.Position,color);
            var core=Math.Max(1,p.Radius-3);
            Instance(coreInstances,i,Vector2.Right*core,Vector2.Down*core,p.Position,new("fff2d5"));
            Instance(tailInstances,i,p.Direction*6,p.Direction.Orthogonal()*p.Radius/2,p.Position-p.Direction*6,color.Darkened(.25f));
        }
        projectileBodies!.Buffer=bodyInstances;projectileBodies.VisibleInstanceCount=projectiles.Count;
        projectileCores!.Buffer=coreInstances;projectileCores.VisibleInstanceCount=projectiles.Count;
        projectileTails!.Buffer=tailInstances;projectileTails.VisibleInstanceCount=projectiles.Count;
        DrawMultimesh(projectileTails,null);DrawMultimesh(projectileBodies,null);DrawMultimesh(projectileCores,null);
    }
    private void FreeProjectileDrawing()
    {
        projectileBodies?.Dispose();projectileCores?.Dispose();projectileTails?.Dispose();projectileCircle?.Dispose();projectileQuad?.Dispose();
    }
}
