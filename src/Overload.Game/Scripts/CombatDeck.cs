using Godot;
using Overload.Domain;

namespace Overload.Game;

/// <summary>A compact combat deck keeps the playfield clear and the action's meaning visible.</summary>
public partial class ArenaHud
{
    private readonly Color teal=new("8cebd3"), violet=new("c7a5fa"), ember=new("efab79");
    private long dispatchAction=-1;
    private float dispatchTime;
    private string dispatchTitle="",dispatchDetail="";
    public void ResetDispatch(){dispatchAction=-1;dispatchTime=0;dispatchTitle=dispatchDetail="";}
    private void ObserveDispatch(double delta)
    {
        if(!arena.Paused&&!arena.LocalMapVisible)dispatchTime=Math.Max(0,dispatchTime-(float)delta);
        if(!arena.Playing||arena.PlayerState.Action is not {} a||a.RootActionId==dispatchAction)return;
        dispatchAction=a.RootActionId;
        if(a.Implementation==ActionImplementation.Base&&a.Traversal==TraversePhase.Standard)return;
        dispatchTime=1.5f;
        dispatchTitle=a.Traversal!=TraversePhase.Standard?"OVERRIDE / "+(a.Traversal==TraversePhase.AnchorPlace?"ANCHOR PLACED":"RETURNED"):"OVERLOAD / "+a.Implementation.ToString().ToUpperInvariant();
        dispatchDetail=a.Traversal!=TraversePhase.Standard?"Traverse has a different rule":string.Join(" + ",a.ConsumedMemories.Select(m=>m.Type))+" → "+SkillName(a.Definition.Id);
    }
    private void DrawFightHud()
    {
        var w=Size.X;var h=Size.Y;var s=arena.PlayerState;
        var title=arena.WorldActive?arena.WorldZone.Name:arena.RewriteLessonActive?"The Memory Chamber":arena.JourneyActive?arena.JourneyName:arena.PracticeTier is not null?"Fracture practice":"The Broken Court";
        var objective=arena.RewriteLessonActive?arena.RewriteLessonTitle:arena.Objective;
        var cardWidth=Math.Min(370,w*.43f);
        Surface(new(18,18,cardWidth,86),new Color("645947"),.88f);
        DrawLine(new(19,20),new(19,102),gold,2);
        Write(new(32,40),Fit(title.ToUpperInvariant(),cardWidth-28,17),17,gold);
        Write(new(32,62),Fit(arena.RewriteLessonActive?"PRACTICE / "+(arena.RewriteLessonStep+1)+" OF 6":arena.WorldActive?Arena.RegionName(arena.WorldZone.Region).ToUpperInvariant():"THE PALIMPSEST",cardWidth-28,10),10,muted);
        Write(new(32,86),Fit(objective.Contains(" / ")?objective[(objective.IndexOf(" / ",StringComparison.Ordinal)+3)..]:objective,cardWidth-28,12),12,ink);
        if(arena.WorldActive)DrawWorldCompass();
        else
        {
            Surface(new(w-178,18,160,58),opacity:.88f);
            Write(new(w-164,40),$"{arena.Controls.Glyph("pause")}  PAUSE",11,muted);
            Write(new(w-164,62),$"{arena.Enemies.Count(e=>!e.Enemy!.Dead)} HOSTILES",12,gold);
        }
        if(s.ElsewhereActive||s.RedCovenantActive)
        {
            var text=s.ElsewhereActive?s.Anchor is {} anchor?$"RETURN ANCHOR · {(anchor.ExpiresAt-s.Tick)/60f:0.0}s":"ELSEWHERE · EVADE → PLACE / RETURN":$"RED COVENANT · FOCUS → LIFE · {s.ReservedPermille/10f:0.#}% RESERVED";
            Write(new(32,124),"OVERRIDE",10,violet);
            Write(new(32,144),Fit(text,Math.Min(430,w*.55f),12),12,ink);
        }
        var bossBody=arena.Enemies.FirstOrDefault(e=>!e.Enemy!.Dead&&e.Enemy.Definition.Role==EnemyRole.Bellkeeper);
        if(bossBody is not null)
        {
            var boss=bossBody.Enemy!;var bw=Math.Min(420,w*.44f);var x=(w-bw)/2;var y=160f;
            CenterWrite(new(w/2,y-10),(boss.Definition.Name??"Bellkeeper").ToUpperInvariant(),14,gold);
            Surface(new(x,y,bw,16),new Color("82705a"),.88f);
            Bar(new(x+3,y+3,bw-6,7),CombatMath.BarBasisPoints(boss.Life,boss.MaximumLife)/10000f,new("b88560"));
            Bar(new(x+3,y+12,bw-6,2),Math.Min(1,boss.Stagger/(float)boss.Definition.StaggerThreshold),teal);
            var tell=bossBody.Stunned?"BROKEN / STRIKE NOW":bossBody.Recovering?"OPENING / COMMIT YOUR ATTACK":bossBody.AttackAge>=0?bossBody.Volley?"VOLLEY / FIND A CLEAR LANE":"HEAVY STRIKE / LEAVE THE ARC":"";
            CenterWrite(new(w/2,y+35),tell,11,bossBody.Stunned||bossBody.Recovering?teal:ember);
        }
        if(arena.ArrivalTime>0&&!MenuVisible&&bossBody is null&&!arena.RewriteLessonActive)
        {
            var alpha=Math.Clamp(arena.ArrivalTime/.7f,0,1)*Math.Clamp((2.6f-arena.ArrivalTime)/.3f,0,1);
            CenterWrite(new(w/2,h*.34f),arena.ArrivalTitle.ToUpperInvariant(),24,new Color(gold,alpha));
        }
        if(dispatchTime>0&&!MenuVisible)
        {
            var alpha=Math.Min(1,dispatchTime*3);
            var y=arena.RewriteLessonActive?h-188:h-211;
            CenterWrite(new(w/2,y),dispatchTitle,17,new Color(teal,alpha));
            CenterWrite(new(w/2,y+23),dispatchDetail,11,new Color(ink,alpha));
        }
        if(arena.RewriteLessonActive)
        {
            Surface(new(30,h-275,w-60,70),new Color("598d83"),.91f);
            WrappedCenteredText(new(44,h-273,w-88,48),arena.RewriteLessonHint,12,ink);
            CenterWrite(new(w/2,h-213),"Practice only / No rewards / Your saved character is preserved",9,muted);
        }
        else if(arena.JourneyActive&&arena.Audio.ShowTutorialHints)
            CenterWrite(new(w/2,h-187),Fit(arena.CombatLesson,w-60,11),11,gold);
        if(arena.CombatNoticeTime>0&&!MenuVisible)
            CenterWrite(new(w/2,h-176),Fit(arena.CombatNotice,w-60,12),12,ember);
        if(arena.WorldActive&&!MenuVisible)
            CenterWrite(new(w/2,h-176),Fit(arena.WorldPrompt,w-60,12),12,gold);
        DrawCombatDeck();
        if(s.Life*4<s.MaximumLife&&!s.Dead)
        {
            var edge=new Color("c86463",arena.Audio.ReducedFlash?.14f:.24f);
            DrawRect(new(0,0,4,h),edge);DrawRect(new(w-4,0,4,h),edge);
            if(s.FlaskCharges>0)Write(new(26,h-144),$"{arena.Controls.Glyph("flask")}  HEAL",12,ember);
        }
        if(arena.Debug)DrawDiagnostics();
    }
    private void DrawCombatDeck()
    {
        var s=arena.PlayerState;var w=Size.X;var h=Size.Y;
        var first=ActionSlotRect(0);var last=ActionSlotRect(5);
        var left=first.Position.X;var right=last.End.X;
        Surface(new(left-13,h-102,right-left+26,94),new Color("6e614d"),.87f);
        var memoryTypes=s.Behavior.Bindings.SelectMany(b=>b.Pattern.RequiredMemories).Concat(new[]{MemoryType.Momentum,MemoryType.Echo}).Distinct().Order().ToArray();
        var total=memoryTypes.Length*104f;var ml=w/2-total/2;
        for(var i=0;i<memoryTypes.Length;i++)
        {
            var type=memoryTypes[i];var token=s.Memories.Get(type);var x=ml+i*104;var color=type==MemoryType.Momentum?teal:type==MemoryType.Echo?violet:gold;
            var c=new Vector2(x+10,h-137);DrawRune(c,type,token is null?muted.Darkened(.35f):color);
            Write(new(x+24,h-138),type.ToString().ToUpperInvariant(),9,token is null?muted:color);
            var hint=token is null?type switch{MemoryType.Momentum=>"Move 3m",MemoryType.Echo=>"Evade a hit",MemoryType.Stillness=>"Hold + hit",_=>"Break stagger"}:$"{s.Memories.RemainingTicks(type)/60f:0.0}s";
            Write(new(x+24,h-122),hint,10,token is null?muted:ink);
            var ratio=token is not null?s.Memories.RemainingTicks(type)/(float)arena.Balance.Memories.LifetimeTicks:type==MemoryType.Momentum?(float)Math.Min(1,s.Memories.MomentumDistance/arena.Balance.Memories.MomentumDistancePixels):0;
            Bar(new(x+24,h-117,72,2),ratio,color);
        }
        var strainColor=s.Strain>75000?ember:gold;
        Write(new(right+21,h-137),"STRAIN",9,muted);
        Write(new(right+21,h-119),$"{s.Strain/1000f:0}",13,strainColor);
        DrawArc(new(right+51,h-133),29,-Mathf.Pi/2,-Mathf.Pi/2+Math.Max(.005f,s.Strain/(float)s.MaximumStrain)*Mathf.Tau,40,strainColor,2);
        Write(new(left-80,h-119),"LV "+CounterText.Short(arena.Character?.State.ValidatedLevel??1),11,gold);
        var names=new[]{"cleave","pulse","lance","special","evade","flask"};var ids=ActionSlots();
        for(var i=0;i<6;i++)
        {
            var id=ids[i];var rect=ActionSlotRect(i);var cd=s.Cooldown(id);
            var prediction=arena.Predictions.GetValueOrDefault(id)?.Selection;
            var overloaded=prediction?.Accepted==true&&prediction.Implementation!=ActionImplementation.Base;
            var active=s.Action?.Definition.Id==id;var color=overloaded?teal:active?gold:new Color("67604f");
            Surface(rect,color,.93f);
            if(overloaded)DrawRect(rect.Grow(-2),new Color(teal,.07f));
            var center=rect.GetCenter();
            DrawActionGlyph(new(center.X,rect.Position.Y+22),id,i,cd>0?muted.Darkened(.45f):overloaded?teal:gold);
            Write(new(rect.Position.X+6,rect.Position.Y+13),Fit(arena.Controls.Glyph(names[i]),rect.Size.X-10,9),9,gold);
            var label=overloaded?prediction!.Implementation.ToString():id==SkillId.Traverse&&s.ElsewhereActive?s.Anchor is null?"Place anchor":"Return":SkillName(id);
            CenterWrite(new(center.X,rect.Position.Y+48),Fit(label,rect.Size.X-10,10),10,overloaded?teal:ink);
            var skill=arena.Balance.Skills.Single(k=>k.Id==id);
            var cost=cd>0?$"{cd/60f:0.0}s":id==SkillId.Flask?$"{s.FlaskCharges} flasks":overloaded?$"+{prediction!.StrainCost/1000} strain":s.RedCovenantActive&&skill.FocusCost>0?$"{skill.FocusCost*.4f:0.#}% Life":skill.FocusCost==0?"Ready":$"{skill.FocusCost} Focus";
            CenterWrite(new(center.X,rect.Position.Y+66),Fit(cost,rect.Size.X-10,9),9,muted);
            if(cd>0&&skill.Cooldown>0)Bar(new(rect.Position.X+5,rect.End.Y-4,rect.Size.X-10,2),1-Math.Min(1,cd/(float)skill.Cooldown),gold);
        }
        DrawReservoir(new(left-69,h-57),shownLife,new("bd5f62"),"LIFE",CombatMath.BarBasisPoints(s.Life,s.MaximumLife)/100+"%");
        DrawReservoir(new(right+68,h-57),s.RedCovenantActive?1-s.ReservedPermille/1000f:shownFocus,s.RedCovenantActive?violet:new("65aaba"),s.RedCovenantActive?"CAPACITY":"FOCUS",s.RedCovenantActive?$"{s.ReservedPermille/10f:0}%":$"{s.Focus/1000}");
        if(arena.Controls.PreservingMemories)CenterWrite(new(w/2,h-160),"MEMORIES HELD / BASE ACTION",11,gold);
    }
    private void DrawReservoir(Vector2 p,float fraction,Color color,string name,string value)
    {
        const float radius=32;
        DrawCircle(p,radius+5,new("0a1217"));DrawArc(p,radius+5,0,Mathf.Tau,64,gold.Darkened(.25f),1);
        DrawCircle(p,radius,new("1b2229"));
        if(fraction>=.999f)DrawCircle(p,radius,color.Darkened(.28f));
        else if(fraction>.001f)
        {
            var start=MathF.Asin(1-2*Math.Clamp(fraction,0,1));var end=Mathf.Pi-start;
            var points=Enumerable.Range(0,49).Select(i=>p+Vector2.FromAngle(Mathf.Lerp(start,end,i/48f))*radius).ToArray();
            DrawColoredPolygon(points,color.Darkened(.28f));
        }
        DrawArc(p,radius-3,.1f,Mathf.Pi*.8f,32,new Color(color,.35f),2);
        CenterWrite(p+new Vector2(0,5),value,15,ink);CenterWrite(p+new Vector2(0,48),name,9,gold);
    }
    private void DrawRune(Vector2 c,MemoryType type,Color color)
    {
        DrawPolyline([c+new Vector2(0,-10),c+new Vector2(8,0),c+new Vector2(0,10),c+new Vector2(-8,0),c+new Vector2(0,-10)],color,1.5f);
        if(type==MemoryType.Momentum)DrawPolyline([c+new Vector2(-3,4),c+new Vector2(3,0),c+new Vector2(-3,-4)],color,2);
        else if(type==MemoryType.Echo)DrawArc(c,4,0,Mathf.Tau,16,color,1.5f);
        else if(type==MemoryType.Stillness)DrawLine(c-new Vector2(0,5),c+new Vector2(0,5),color,2);
        else DrawPolyline([c+new Vector2(-3,-5),c+new Vector2(2,0),c+new Vector2(-2,2),c+new Vector2(3,5)],color,2);
    }
    private void WrappedCenteredText(Rect2 rect,string text,int size,Color color)
    {
        var lines=new List<string>();var line="";
        foreach(var word in text.Split(' '))
        {
            var next=line.Length==0?word:line+" "+word;
            if(ThemeDB.FallbackFont.GetStringSize(next,fontSize:FontSize(size)).X>rect.Size.X&&line.Length>0){lines.Add(line);line=word;}
            else line=next;
        }
        if(line.Length>0)lines.Add(line);
        var lineHeight=FontSize(size)+5;
        var baseline=rect.Position.Y+(rect.Size.Y-lines.Count*lineHeight)/2+FontSize(size);
        for(var i=0;i<lines.Count;i++)CenterWrite(new(rect.GetCenter().X,baseline+i*lineHeight),lines[i],size,color);
    }
    private void DrawActionGlyph(Vector2 p,SkillId skill,int slot,Color color)
    {
        if(skill==SkillId.Needle)
        {DrawLine(p+new Vector2(-9,9),p+new Vector2(9,-9),color,2);DrawArc(p+new Vector2(4,-4),3,0,Mathf.Tau,12,color,1);}
        else if(skill is SkillId.ShardShot or SkillId.BoneVolley)
        {for(var i=0;i<3;i++){var q=p+new Vector2((i-1)*6,Math.Abs(i-1)*3);DrawPolyline([q+new Vector2(0,-8),q+new Vector2(3,1),q+new Vector2(0,6),q+new Vector2(-2,0),q+new Vector2(0,-8)],color,1);}}
        else if(skill is SkillId.EmberWell or SkillId.StormLoom or SkillId.CinderOrb)
        {DrawArc(p+new Vector2(0,5),9,0,Mathf.Tau,20,color,1.5f);DrawPolyline([p+new Vector2(-4,2),p+new Vector2(-5,-4),p+new Vector2(0,-11),p+new Vector2(5,-2),p+new Vector2(4,3)],color,2);}
        else if(skill==SkillId.Tether)
        {DrawCircle(p-new Vector2(7,5),3,color);DrawArc(p+new Vector2(7,5),4,0,Mathf.Tau,16,color,1.5f);DrawLine(p-new Vector2(5,3),p+new Vector2(5,3),color,2);}
        else if(skill==SkillId.EchoOrder)
        {DrawRune(p-new Vector2(5,0),MemoryType.Echo,new Color(color,.5f));DrawRune(p+new Vector2(5,0),MemoryType.Echo,color);}
        else if(skill==SkillId.Reap)
        {DrawLine(p+new Vector2(-6,9),p+new Vector2(4,-7),color,2);DrawArc(p+new Vector2(0,1),10,-Mathf.Pi*.9f,-.1f,20,color,2);}
        else if(skill is SkillId.Veil or SkillId.RecallThread)
        {DrawArc(p,10,-Mathf.Pi*.85f,Mathf.Pi*.85f,24,color,2);DrawLine(p-new Vector2(2,5),p+new Vector2(2,5),color,2);}
        else DrawSkillIcon(p,slot,color);
    }
}
