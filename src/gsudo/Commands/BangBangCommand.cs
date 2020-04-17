﻿using gsudo.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace gsudo.Commands
{
    class BangBangCommand : ICommand
    {
        public string Pattern { get; internal set; }

        public Task<int> Execute()
        {
            var caller = Process.GetCurrentProcess().GetParentProcessExcludingShim().MainModule.ModuleName;
            var length = (int)NativeMethods.GetConsoleCommandHistoryLength(caller);

            if (length == 0)
                throw new ApplicationException("Failed to find last invoked command (GetConsoleCommandHistoryLength==0)");

            IntPtr CommandBuffer = Marshal.AllocHGlobal(length);
            var ret = NativeMethods.GetConsoleCommandHistory(CommandBuffer, length, caller);

            if (ret == 0)
                throw new ApplicationException($"Failed to find last invoked command (GetConsoleCommandHistory=0; LastErr={Marshal.GetLastWin32Error()})");

            var commandHistory = Marshal.PtrToStringAuto(CommandBuffer, length / 2).Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);

            string commandToElevate;

            if (Pattern=="!!")
            { 
                commandToElevate = commandHistory
                    .Reverse() // look for last commands first
                    .Skip(1) // skip gsudo call
                    .FirstOrDefault();
            }
            else
            {
                var lookup = Pattern.Substring(1);
                commandToElevate = commandHistory
                    .Reverse() // look for last commands first
                    .Skip(1) // skip gsudo call
                    .FirstOrDefault(s => s.StartsWith(lookup));
            }

            if (commandToElevate == null)
                throw new ApplicationException("Failed to find last invoked command in history.");

            Logger.Instance.Log("Command to run: " + commandToElevate, LogLevel.Info);

            return new RunCommand()
            { CommandToRun = ArgumentsHelper.SplitArgs(commandToElevate) }
            .Execute();
        }

        class NativeMethods
        {
            // Many thanks to comment from eryk sun for posting here: https://www.hanselman.com/blog/ForgottenButAwesomeWindowsCommandPromptFeatures.aspx
            // (Otherwise I wouldnt be able to find this undocumented api.)

            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern UInt32 GetConsoleCommandHistoryLength(string ExeName);

            [DllImport("kernel32.dll", SetLastError = true, CharSet =CharSet.Unicode)]
            public static extern UInt32 GetConsoleCommandHistory(
                                 IntPtr CommandBuffer,
                                 int CommandBufferLength,
                                 string ExeName);

        }
    }
}
