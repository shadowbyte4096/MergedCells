namespace MergedCells.Core.Tests;

// The sequences are the ones Godot 4.7.2 sent on the Android emulator in chunk 11: a go-back request for every back key
// event (down, each repeat, up), interleaved with the key events themselves, numbered by process frame
public class BackPressesTests
{
    [Fact]
    public void A_short_press_whose_request_comes_before_the_key_counts_once()
    {
        var presses = new BackPresses();

        var first = presses.Request(293);
        presses.KeyDown(293);
        presses.KeyUp(293);
        var second = presses.Request(293);

        Assert.Equal((true, false), (first, second));
    }

    [Fact]
    public void A_short_press_whose_key_comes_first_counts_once()
    {
        var presses = new BackPresses();

        presses.KeyDown(10);
        var first = presses.Request(10);
        presses.KeyUp(10);
        var second = presses.Request(10);

        Assert.Equal((true, false), (first, second));
    }

    [Fact]
    public void A_long_press_counts_once_through_its_repeats_and_release()
    {
        var presses = new BackPresses();

        presses.KeyDown(3784);
        var down = presses.Request(3784);
        var repeat = presses.Request(3809);
        presses.KeyUp(3809);
        var up = presses.Request(3809);

        Assert.Equal((true, false, false), (down, repeat, up));
    }

    [Fact]
    public void A_long_press_whose_first_request_came_before_the_key_counts_once()
    {
        var presses = new BackPresses();

        var first = presses.Request(5);
        presses.KeyDown(5);
        var repeat = presses.Request(30);
        presses.KeyUp(30);
        var up = presses.Request(30);

        Assert.Equal((true, false, false), (first, repeat, up));
    }

    [Fact]
    public void A_press_whose_only_request_comes_on_release_counts()
    {
        var presses = new BackPresses();

        presses.KeyDown(7);
        presses.KeyUp(9);

        Assert.True(presses.Request(9));
    }

    [Fact]
    public void Requests_without_key_events_each_count()
    {
        var presses = new BackPresses();

        Assert.Equal((true, true), (presses.Request(1), presses.Request(2)));
    }

    [Fact]
    public void Two_presses_count_twice()
    {
        var presses = new BackPresses();
        presses.Request(293);
        presses.KeyDown(293);
        presses.KeyUp(293);
        presses.Request(293);

        var next = presses.Request(400);
        presses.KeyDown(400);
        presses.KeyUp(400);
        var duplicate = presses.Request(400);

        Assert.Equal((true, false), (next, duplicate));
    }

    [Fact]
    public void Reset_forgets_a_press_whose_release_was_lost()
    {
        var presses = new BackPresses();
        presses.KeyDown(10);
        presses.Request(10);

        presses.Reset();

        Assert.True(presses.Request(50));
    }

    [Fact]
    public void A_request_a_frame_after_the_release_counts()
    {
        var presses = new BackPresses();
        presses.KeyDown(10);
        presses.Request(10);
        presses.KeyUp(10);

        Assert.True(presses.Request(11));
    }
}
