﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Utility;
using ArmA.Studio.Data;
using System.Xml;

namespace ArmA.Studio
{
    public sealed class BreakpointManager : IEnumerable<BreakpointInfo>
    {
        public event EventHandler OnBreakPointsChanged;

        private Dictionary<ProjectFileFolder, List<BreakpointInfo>> BreakPointDictionary;

        public BreakpointManager()
        {
            this.BreakPointDictionary = new Dictionary<ProjectFileFolder, List<BreakpointInfo>>();
        }

        public IEnumerable<BreakpointInfo> this[ProjectFileFolder pff]
        {
            get { return this.BreakPointDictionary[pff]; }
        }

        public BreakpointInfo SetBreakpoint(ProjectFileFolder pff, int line) => this.SetBreakpoint(pff, new BreakpointInfo() { FileFolder = pff, IsEnabled = true, Line = line, SqfCondition = null});
        public BreakpointInfo SetBreakpoint(ProjectFileFolder pff, BreakpointInfo bpi)
        {
            bpi.FileFolder = pff;
            var bpiList = this.BreakPointDictionary[pff];
            var index = bpiList.FindIndex((item) => item.Line == bpi.Line);
            if (index == -1)
            {
                bpiList.Add(bpi);
            }
            else
            {
                bpiList[index] = bpi;
            }
            this.OnBreakPointsChanged?.Invoke(this, new EventArgs());
            return bpi;
        }
        public BreakpointInfo GetBreakpoint(ProjectFileFolder pff, int line)
        {
            var bpiList = this.BreakPointDictionary[pff];
            var index = bpiList.FindIndex((item) => item.Line == line);
            if (index == -1)
            {
                return default(BreakpointInfo);
            }
            else
            {
                return bpiList[index];
            }
        }
        public void RemoveBreakpoint(ProjectFileFolder pff, BreakpointInfo bpi) => this.RemoveBreakpoint(pff, bpi.Line);
        public void RemoveBreakpoint(ProjectFileFolder pff, int line)
        {
            var bpiList = this.BreakPointDictionary[pff];
            var index = bpiList.FindIndex((item) => item.Line == line);
            if (index == -1)
            {
                bpiList.RemoveAt(index);
                this.OnBreakPointsChanged?.Invoke(this, new EventArgs());
            }
        }

        public IEnumerator<BreakpointInfo> GetEnumerator()
        {
            return this.BreakPointDictionary.SelectMany((kvp) => kvp.Value).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator().Cast();
        }

        public void LoadBreakpoints(System.IO.Stream stream)
        {
            var reader = XmlReader.Create(stream);
            ProjectFileFolder currentFile = null;
            while (reader.Read())
            {
                try
                {
                    if (reader.Name.Equals("breakpointinfo") && currentFile != null)
                    {
                        var bpi = new BreakpointInfo();
                        bpi.FileFolder = currentFile;
                        if (reader.MoveToFirstAttribute())
                        {
                            do
                            {
                                switch (reader.Name)
                                {
                                    case nameof(BreakpointInfo.IsEnabled):
                                        bpi.IsEnabled = reader.Value.Equals("true", StringComparison.InvariantCultureIgnoreCase);
                                        break;
                                    case nameof(BreakpointInfo.Line):
                                        bpi.Line = int.Parse(reader.Value);
                                        break;
                                    case nameof(BreakpointInfo.SqfCondition):
                                        bpi.SqfCondition = reader.Value;
                                        break;
                                }
                            } while (reader.MoveToNextAttribute());
                        }
                        
                    }
                    else if(reader.Name.Equals("file"))
                    {
                        currentFile = Workspace.Instance.GetProjectFileFolderReference(new Uri(reader.Value, UriKind.RelativeOrAbsolute));
                    }
                }
                catch { }
            }

            this.OnBreakPointsChanged?.Invoke(this, new EventArgs());
        }
        public void SaveBreakpoints(System.IO.Stream stream)
        {
            var writer = XmlWriter.Create(stream);

            foreach (var kvp in this.BreakPointDictionary)
            {
                if (!kvp.Value.Any())
                    continue;
                writer.WriteStartElement("file");
                writer.WriteAttributeString("path", kvp.Key.FilePath);

                foreach(var bpi in kvp.Value)
                {
                    writer.WriteStartElement("breakpointinfo");
                    writer.WriteAttributeString(nameof(bpi.IsEnabled), bpi.IsEnabled.ToString());
                    writer.WriteAttributeString(nameof(bpi.Line), bpi.Line.ToString());
                    if (bpi.SqfCondition != null)
                    {
                        writer.WriteAttributeString(nameof(bpi.SqfCondition), bpi.SqfCondition);
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            stream.Flush();
        }
    }
}