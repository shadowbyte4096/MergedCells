using Godot;

namespace MergedCells;

// The startup intro, played once over the first mode select
public partial class Main
{
    private IntroLayer intro;

    [Export] public PackedScene IntroScene { get; set; }

    private void StartIntro()
    {
        intro = IntroScene.Instantiate<IntroLayer>();
        intro.Menu = () => screen as ModeSelect;
        intro.Finished += () =>
        {
            intro = null;
            GD.Print("Intro: finished");
        };
        AddChild(intro);
        GD.Print("Intro: started");
    }
}
