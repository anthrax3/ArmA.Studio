﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Utility;
using Utility.Collections;

namespace ArmA.Studio.Data
{
    public class Solution : IXmlSerializable
    {
        public static class XmlHelper
        {
            public static void Serialize_Project(XmlWriter writer, Project p)
            {
                writer.WriteStartElement(nameof(Project));


                writer.WriteAttributeString(nameof(Project.ArmAPath), p.ArmAPath);
                writer.WriteAttributeString(nameof(Project.Name), p.Name);
                writer.WriteAttributeString(nameof(Project.FilePath), p.FilePath);
                writer.WriteAttributeString(nameof(Project.ProjectType), Enum.GetName(typeof(EProjectType), p.ProjectType));

                foreach(var pff in p)
                {
                    Serialize_ProjectFileFolder(writer, pff);
                }


                writer.WriteEndElement();
            }
            public static Project Deserialize_Project(Solution sol, XmlReader reader)
            {
                if (reader.Name != nameof(Project))
                    throw new XmlException($"Invalid {nameof(Project)}");


                var p = new Project();

                p.ArmAPath = reader.GetAttribute(nameof(Project.ArmAPath));
                p.Name = reader.GetAttribute(nameof(Project.Name));
                p.FilePath = reader.GetAttribute(nameof(Project.FilePath));
                EProjectType tmp;
                if (!Enum.TryParse(reader.GetAttribute(nameof(Project.ProjectType)), out tmp))
                    throw new XmlException($"Invalid {nameof(Project.ProjectType)}");
                p.ProjectType = tmp;
                if (reader.IsEmptyElement)
                {
                    reader.ReadStartElement(nameof(Project));
                }
                else
                {
                    reader.ReadStartElement(nameof(Project));
                    while (reader.Name == nameof(ProjectFile))
                    {
                        p.Children.Add(Deserialize_ProjectFileFolder(reader));
                    }
                    reader.ReadEndElement();
                }
                p.OwningSolution = sol;
                foreach (var pff in p)
                {
                    pff.OwningSolution = sol;
                    pff.OwningProject = p;
                }
                p.DeserializeCallback();
                return p;
            }

            public static void Serialize_ProjectFileFolder(XmlWriter writer, ProjectFile pff)
            {
                writer.WriteStartElement(nameof(ProjectFile));
                
                writer.WriteAttributeString(nameof(ProjectFile.ProjectRelativePath), pff.ProjectRelativePath);

                writer.WriteEndElement();
            }
            public static ProjectFile Deserialize_ProjectFileFolder(XmlReader reader)
            {
                if (reader.Name != nameof(ProjectFile) || !reader.IsEmptyElement)
                    throw new XmlException($"Invalid {nameof(ProjectFile)}");


                var pff = new ProjectFile();

                pff.ProjectRelativePath = reader.GetAttribute(nameof(ProjectFile.ProjectRelativePath));

                reader.ReadStartElement(nameof(ProjectFile));
                return pff;
            }
        }

        public ObservableSortedCollection<Project> Projects { get; private set; }
        public Uri FileUri { get; set; }

        public Solution()
        {
            this.Projects = new ObservableSortedCollection<Project>();
            this.Projects.CollectionChanged += Projects_CollectionChanged;
        }

        private static void Projects_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (Project it in e.NewItems)
                {
                    it.AddedCallback();
                }
            }
            if (e.OldItems != null)
            {
                foreach (Project it in e.OldItems)
                {
                    it.RemovedCallback();
                }
            }
        }

        #region Xml Serialization
        public static void Serialize(Solution solution, Stream stream)
        {
            var serializer = new XmlSerializer(typeof(Solution));
            serializer.Serialize(stream, solution);
        }
        public static Solution Deserialize(Stream stream, Uri fileUri)
        {
            var serializer = new XmlSerializer(typeof(Solution));
            var sol = serializer.Deserialize(stream) as Solution;
            sol.FileUri = fileUri;
            return sol;
        }

        XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            if (reader.IsEmptyElement)
                return;
            reader.ReadStartElement(nameof(Solution));

            while (reader.Name == nameof(Project))
            {
                this.Projects.Add(XmlHelper.Deserialize_Project(this, reader));
            }

            reader.ReadEndElement();
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            foreach(var p in this.Projects)
            {
                XmlHelper.Serialize_Project(writer, p);
            }
        }
        #endregion

        public ProjectFile FindFileFolder(Uri uri)
        {
            foreach (var proj in this.Projects)
            {
                var ff = proj.FindFileFolder(uri);
                if (ff != null)
                    return ff;
            }
            return null;
        }

        /// <summary>
        /// Tries to find a <see cref="ProjectFile"/> for provided ArmA-Path.
        /// will return null object if nothing was found.
        /// </summary>
        /// <param name="armaPath">ArmA Path of the <see cref="ProjectFile"/> to find.</param>
        /// <returns>The correct <see cref="ProjectFile"/> instance or null if no corresponding file was found.</returns>
        public ProjectFile GetProjectFileFromArmAPath(string armaPath)
        {
            foreach (var project in this.Projects)
            {
                var pff = project.GetFileFromArmAPath(armaPath);
                if (pff != null)
                    return pff;
            }
            return null;
        }

        public void AddProject(string projectName, EProjectType type) => this.AddProject(projectName, type, Path.Combine(Path.GetDirectoryName(this.FileUri.LocalPath), projectName));
        public void AddProject(string projectName, EProjectType type, string path)
        {
            var project = new Project() { Name = projectName, ProjectType = type, FilePath = path, OwningSolution = this };
            if (Directory.Exists(path))
            {
                project.Scan();
            }
            this.Projects.Add(project);
        }
    }
}