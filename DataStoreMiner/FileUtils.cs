using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace DatastoreMiner
{
    public enum FileFilterEnum { Largest, Smallest, Top, Bottom, Pattern }

    /// <summary>
    /// Utilities for filtering files to sort out those that might contain data
    /// </summary>
    public class FileFilter
    {
        public FileFilterEnum FilterType;
        public Regex regex;

        public FileFilter(FileFilterEnum type, string strregex)
        {
            FilterType = type;
            if (FilterType == FileFilterEnum.Pattern) this.regex = new Regex(strregex);
        }

        /// <summary>
        /// Take a list of files and filter according to the FileFilter options.
        /// Largest: return the biggest file
        /// Smallest: return the smallest file
        /// Top: return all the files in the top directory of the hierarchy
        /// Bottom: return all the files in the deepest directories of the hierarchy
        /// Pattern: return all the files with names matching a Regex
        /// </summary>
        /// <param name="FileInfos"></param>
        /// <returns></returns>
        public FileInfo[] FilterFiles(FileInfo[] FileInfos)
        {
            FileInfo[] Result = null;
            FileInfo fi;
            switch (FilterType)
            {
                case FileFilterEnum.Largest:
                    fi = Largest(FileInfos);
                    Result = new FileInfo[] { fi };
                    break;
                case FileFilterEnum.Smallest:
                    fi = Largest(FileInfos);
                    Result = new FileInfo[] { fi };
                    break;
                case FileFilterEnum.Top:
                    Result = Top(FileInfos);
                    break;
                case FileFilterEnum.Bottom:
                    Result = Bottom(FileInfos);
                    break;
                case FileFilterEnum.Pattern:
                    Result = Pattern(FileInfos);
                    break;
            }
            return Result;
        }

        /// <summary>
        /// Smallest file in the list
        /// </summary>
        /// <param name="FileInfos"></param>
        /// <returns></returns>
        public FileInfo Smallest(FileInfo[] FileInfos)
        {
            FileInfo Result = null;
            foreach (FileInfo fi in FileInfos)
            {
                if ((Result==null)||(fi.Length<Result.Length))
                {
                    Result = fi;
                }
            }
            return Result;
        }

        /// <summary>
        /// Largest file in the list
        /// </summary>
        /// <param name="FileInfos"></param>
        /// <returns></returns>
        public FileInfo Largest(FileInfo[] FileInfos)
        {
            FileInfo Result = null;
            foreach (FileInfo fi in FileInfos)
            {
                if ((Result == null) || (fi.Length > Result.Length))
                {
                    Result = fi;
                }
            }
            return Result;
        }

        /// <summary>
        /// List of files at the top of the hierarchy, not necessarily in the same folder, but at the same level
        /// e.g. one/a, one/b and two/c, two/d returns [one/a,one/b,two/c,two/d] as they're all at level one.
        /// </summary>
        /// <param name="FileInfos"></param>
        /// <returns></returns>
        public FileInfo [] Top(FileInfo[] FileInfos)
        {
            //Result is probably FileInfos[0] if the directory scan was sensible, but can't rely on that so use the full path name.
            //Also, there can be more than one file in the top directory, so we have to return a list
            List<FileInfo> Result = new List<FileInfo>();
            int MinDirs = 0;
            foreach (FileInfo fi in FileInfos)
            {
                string [] Dirs = fi.FullName.Split(new string [] { @"\/" }, StringSplitOptions.RemoveEmptyEntries);
                if ((Result.Count == 0) || (Dirs.Length < MinDirs))
                {
                    if (Dirs.Length < MinDirs) Result = new List<FileInfo>(); //if it's higher up than existing file list, then clear and start again
                    Result.Add(fi);
                    MinDirs = Dirs.Length;
                }
            }
            return Result.ToArray();
        }

        /// <summary>
        /// Files at the bottom of the hierarchy (see note on sibling folders for Top)
        /// </summary>
        /// <param name="FileInfos"></param>
        /// <returns></returns>
        public FileInfo [] Bottom(FileInfo[] FileInfos)
        {
            List<FileInfo> Result = new List<FileInfo>();
            int MaxDirs = int.MaxValue;
            foreach (FileInfo fi in FileInfos)
            {
                string[] Dirs = fi.FullName.Split(new string[] { @"\/" }, StringSplitOptions.RemoveEmptyEntries);
                if ((Result.Count == 0) || (Dirs.Length < MaxDirs))
                {
                    if (Dirs.Length > MaxDirs) Result = new List<FileInfo>(); //if it's lower than existing file list, then clear and start again
                    Result.Add(fi);
                    MaxDirs = Dirs.Length;
                }
            }
            return Result.ToArray();
        }

        /// <summary>
        /// Files with the name matching a regex
        /// </summary>
        /// <param name="FileInfos"></param>
        /// <returns></returns>
        public FileInfo[] Pattern(FileInfo[] FileInfos)
        {
            List<FileInfo> Result = new List<FileInfo>();

            foreach (FileInfo fi in FileInfos)
            {
                if (regex.Match(fi.FullName).Success) Result.Add(fi);
            }

            return Result.ToArray();
        }
    }
}
