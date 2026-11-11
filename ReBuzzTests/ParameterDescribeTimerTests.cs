using AwesomeAssertions;
using BuzzGUI.Interfaces;
using ReBuzz.Core;
using ReBuzzTests.Automation;
using System;

namespace ReBuzzTests
{
    /// <summary>
    /// ParameterCore throttles description-change events with a per-parameter timer. Only a
    /// parameter whose changed track has description subscribers may start its timer, and the
    /// timer must stop once the pending events have been raised. Previously a static flag
    /// shared by all parameters made other parameters start timers that never stopped.
    /// </summary>
    public class ParameterDescribeTimerTests
    {
        static ParameterCore NewParameter() => new ParameterCore(new FakeDispatcher());

        [Test]
        public void ParameterWithoutDescriptionSubscribersNeverStartsItsTimer()
        {
            var watched = NewParameter();
            var other = NewParameter();
            Action<IParameter, int> handler = (p, t) => { };
            watched.SubscribeEvents(0, handler, handler);

            watched.InvokeEvents(null, 0);
            watched.IsDescribeTimerRunning.Should().BeTrue();

            // While the watched parameter has an event pending, other parameters change.
            for (int i = 0; i < 32; i++)
                other.InvokeEvents(null, i % 16);

            other.IsDescribeTimerRunning.Should().BeFalse("it has no description subscribers");

            watched.RaisePendingDescriptionEvents();
            watched.IsDescribeTimerRunning.Should().BeFalse("the timer stops after raising its events");
        }

        [Test]
        public void RemovingTheSubscriberWhileAnEventIsPendingLeavesNoTimerRunning()
        {
            var watched = NewParameter();
            var other = NewParameter();
            Action<IParameter, int> handler = (p, t) => { };
            watched.SubscribeEvents(0, handler, handler);

            watched.InvokeEvents(null, 0);
            watched.UnsubscribeEvents(0, handler, handler);
            watched.RaisePendingDescriptionEvents();

            watched.InvokeEvents(null, 0);
            other.InvokeEvents(null, 0);

            watched.IsDescribeTimerRunning.Should().BeFalse();
            other.IsDescribeTimerRunning.Should().BeFalse();
        }

        [Test]
        public void EachPendingTrackIsRaisedOnceWhenTheTimerFires()
        {
            var parameter = NewParameter();
            int calls = 0;
            Action<IParameter, int> handler = (p, t) => calls++;
            parameter.SubscribeEvents(0, handler, handler);
            parameter.SubscribeEvents(1, handler, handler);

            parameter.InvokeEvents(null, 0);
            parameter.InvokeEvents(null, 0);
            parameter.InvokeEvents(null, 1);
            parameter.InvokeEvents(null, 0);
            int callsBeforeTimer = calls;

            parameter.RaisePendingDescriptionEvents();

            (calls - callsBeforeTimer).Should().Be(2, "tracks 0 and 1 were pending, each raised once");

            parameter.RaisePendingDescriptionEvents();
            (calls - callsBeforeTimer).Should().Be(2, "nothing is pending any more");
        }
    }
}
