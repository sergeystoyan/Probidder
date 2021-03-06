﻿using System.Collections.Generic;
using System.Linq;
using System.IO;

namespace Cliver
{
    public class FileSystemRoutines
    {
        public static bool IsCaseSensitive
        {
            get
            {
                if (isCaseSensitive == null)
                {
                    var tmp = Path.GetTempPath();
                    isCaseSensitive = !Directory.Exists(tmp.ToUpper()) || !Directory.Exists(tmp.ToLower());
                }
                return (bool)isCaseSensitive;
            }
        }
        static bool? isCaseSensitive = null;

        static public List<string> GetFiles(string directory, bool include_subfolders = true)
        {
            List<string> fs = Directory.EnumerateFiles(directory).ToList();
            if (include_subfolders)
                foreach (string d in Directory.EnumerateDirectories(directory))
                    fs.AddRange(GetFiles(d));
            return fs;
        }

        public static string CreateDirectory(string directory, bool unique = false)
        {
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            else if (unique)
            {
                int i = 1;
                string p = directory + "_" + i;
                for (; Directory.Exists(p); p = directory + "_" + (++i)) ;
                directory = p;
                Directory.CreateDirectory(directory);
            }
            return directory;
        }

        public static void ClearDirectory(string directory, bool recursive = true)
        {
            if(!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                return;
            }

            foreach (string file in Directory.GetFiles(directory))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            if (recursive)
                foreach (string d in Directory.GetDirectories(directory))
                    DeleteDirectory(d, recursive);
        }

        public static void DeleteDirectory(string directory, bool recursive = true)
        {
            foreach (string file in Directory.GetFiles(directory))
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            if (recursive)
                foreach (string d in Directory.GetDirectories(directory))
                    DeleteDirectory(d, recursive);
            Directory.Delete(directory, false);
        }

        public static bool DeleteDirectorySteadfastly(string directory, bool recursive = true)
        {
            bool error = false;
            foreach (string file in Directory.GetFiles(directory))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch
                {
                    error = true;
                }
            }
            if (recursive)
                foreach (string d in Directory.GetDirectories(directory))
                    DeleteDirectorySteadfastly(d, recursive);
            try
            {
                Directory.Delete(directory, false);
            }
            catch
            {
                error = true;
            }
            return !error;
        }

        public static void CopyFile(string file1, string file2, bool overwrite = false)
        {
            CreateDirectory(PathRoutines.GetFileDir(file2), false);
            File.Copy(file1, file2, overwrite);
        }

        public static void MoveFile(string file1, string file2, bool overwrite = true)
        {
            CreateDirectory(PathRoutines.GetFileDir(file2), false);
            if (File.Exists(file2))
            {
                if (!overwrite)
                    throw new System.Exception("File " + file2 + " already exists.");
                File.Delete(file2);
            }
            File.Move(file1, file2);
        }

        //public static void Copy(string path1, string path2, bool overwrite = false)
        //{
        //    if (Directory.Exists(path1))
        //    {
        //        CreateDirectory(path2, false);
        //        foreach (string f in Directory.GetFiles(path1))
        //        {
        //            string f2 = PathRoutines.GetPathMirroredInDir(f, path1, path2);
        //            File.Copy(f, f2, overwrite);
        //        }
        //        foreach (string d in Directory.GetDirectories(path1))
        //        {
        //            string d2 = PathRoutines.GetPathMirroredInDir(d, path1, path2);
        //            Copy(d, d2, overwrite);
        //        }
        //        CreateDirectory(PathRoutines.GetDirFromPath(path2), false);
        //    }
        //    else
        //    {
        //        string f2 = PathRoutines.GetPathMirroredInDir(f, path1, path2);
        //        File.Copy(f, f2, overwrite);
        //    }
        //}

        public static bool IsFileLocked(string file)
        {
            try
            {
                using (Stream stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None))
                    return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
