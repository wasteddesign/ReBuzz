using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BuzzGUI.Common;

namespace BuzzGUI.WaveformControl.Commands
{
    public class SnapToZeroCrossingCommand : NoopCommand
	{
        public SnapToZeroCrossingCommand(WaveformVM waveformVm) : base(waveformVm) { }

		public override void Execute(object parameter)
		{
			if (UpdateFromParam(parameter))
			{
                int inStart, inEnd, dist, curStart, curEnd;
                Nullable<bool> isRising = null; // true for -0+, false for +0-
                Nullable<bool> startFirst = null; // true for start, false for end
                bool isCompleted = false;
                bool startFound = false;
                bool endFound = false;
                
                TemporaryWave wave = new TemporaryWave(WaveformVm.Waveform);

                inStart = curStart = Selection.StartSample;
                inEnd = curEnd = Selection.EndSample;
                dist = 0;

                // check for starting at an endpoint
                if(inStart == 0)
                    startFirst = isRising = true;
                if(inEnd == wave.SampleCount)
                    startFirst = isRising = false;

                // find the largest distance to sample end points
                int longestEdge;

                if((wave.SampleCount - inEnd) > inStart)
                    longestEdge = wave.SampleCount - inEnd;
                else
                    longestEdge = inStart;

                // search for zero crossings
                for(int i = 0; (i < (longestEdge-1)) && !isCompleted; i++) {

                    // check for hitting end points with out having a matching endpoint
                    if (((inStart - i) == 0) && (startFirst != true))
                        // exit loop
                        break;

                    if (((inEnd + i) == wave.SampleCount) && (startFirst != false))
                        // exit loop
                        break;

                    // check for first crossing, if one found, remember direction
                    if (startFirst == null)
                    {
                        // check start then end
                        if ((wave.Left[inStart - i - 1] < 0) && (wave.Left[inStart - i] > 0))
                        {
                            // start is rising
                            isRising = true;
                            startFirst = true;
                            curStart = inStart - dist;
                        }
                        else if ((wave.Left[inStart - i - 1] > 0) && (wave.Left[inStart - i] < 0))
                        {
                            // start is falling
                            isRising = false;
                            startFirst = true;
                            curStart = inStart - dist;
                        }
                        else if ((wave.Left[inEnd + i] < 0) && (wave.Left[inEnd + i + 1] > 0))
                        {
                            // end is rising
                            isRising = true;
                            startFirst = false;
                            curEnd = inEnd + dist;
                        }
                        else if ((wave.Left[inEnd + i] > 0) && (wave.Left[inEnd + i + 1] < 0))
                        {
                            // end is falling
                            isRising = false;
                            startFirst = false;
                            curEnd = inEnd + dist;
                        }
                    }
                    // if second of same direction is found, change selection and break
                    else
                    {
                        // look for second crossing
                        if (startFirst == true)
                        {
                            // check for zero crossings at end
                            if ((isRising == true) && (wave.Left[inEnd + i] < 0) && (wave.Left[inEnd + i + 1] > 0))
                            {
                                // end is rising
                                curEnd = inEnd + dist;
                                isCompleted = true;
                            }
                            else if ((isRising == false) && (wave.Left[inEnd + i] > 0) && (wave.Left[inEnd + i + 1] < 0))
                            {
                                // end is falling
                                curEnd = inEnd + dist;
                                isCompleted = true;
                            }
                        }
                        else
                        {
                            // check for zero crossings at start
                            if ((isRising == true) && (wave.Left[inStart - i - 1] < 0) && (wave.Left[inStart - i] > 0))
                            {
                                // start is rising
                                curStart = inStart - dist;
                                isCompleted = true;
                            }
                            else if ((isRising == false) && (wave.Left[inStart - i - 1] > 0) && (wave.Left[inStart - i] < 0))
                            {
                                // start is falling
                                curStart = inStart - dist;
                                isCompleted = true;
                            }
                        }
                    }

                    dist++;
                }

                // update selection
                if (isCompleted == true)
                {
                    Selection.StartSample = curStart;
                    Selection.EndSample = curEnd;
                }
			}
		}
    }
}
