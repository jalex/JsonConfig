//
// Copyright (C) 2012 Timo Dörr
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
//
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace JsonConfig {

    public static class Config {

        public static readonly dynamic Default = new ConfigObject();
        public static readonly dynamic User = new ConfigObject();
        public static readonly dynamic Global;

        static Config() {
            // static C'tor, run once to check for compiled/embedded config
            var domain = AppDomain.CurrentDomain;


            // scan ALL linked assemblies and merge their default configs while giving the entry assembly top priority in merge
            var entryAssembly = Assembly.GetEntryAssembly();
            var assemblies = domain.GetAssemblies().Where(x => !x.IsDynamic);
            foreach(var assembly in assemblies.Where(assembly => !assembly.Equals(entryAssembly))) {
                Default = Merger.Merge(GetDefaultConfig(assembly), Default);
            }
            if(entryAssembly != null) {
                Default = Merger.Merge(GetDefaultConfig(entryAssembly), Default);
            }

            var paths = new List<string>();
            if(domain.SetupInformation.ShadowCopyDirectories != null) {
                var dirs = domain.SetupInformation.ShadowCopyDirectories.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                paths.AddRange(dirs);
            }
            if(paths.Count == 0) paths.Add(domain.BaseDirectory);

            foreach(var path in paths) {
                FileInfo[] files;
                try {
                    files = new DirectoryInfo(path).GetFiles();
                } catch {
                    continue;
                }
                var configFiles = files.Where(fi =>
                    fi.FullName.EndsWith("Settings.conf.json", StringComparison.OrdinalIgnoreCase) ||
                    fi.FullName.EndsWith("Settings.conf", StringComparison.OrdinalIgnoreCase) ||
                    fi.FullName.EndsWith("Settings.json", StringComparison.OrdinalIgnoreCase)
                );
                foreach(var configFile in configFiles) {
                    var config = ParseJson(File.ReadAllText(configFile.FullName));
                    User = Merger.Merge(config, User);
                }
            }

            Global = Merger.Merge(User, Default);
        }

        ///// <summary>
        /////     Gets a ConfigObject that represents the current configuration. Since it is
        /////     a cloned copy, changes to the underlying configuration files that are done
        /////     after GetCurrentScope() is called, are not applied in the returned instance.
        ///// </summary>
        //public static ConfigObject GetCurrentScope() {
        //    if(Global is NullExceptionPreventer) return new ConfigObject();
        //    return Global.Clone();
        //}

        //public static ConfigObject ApplyJsonFromFileInfo(FileInfo file, ConfigObject config = null) {
        //    var overlayJson = File.ReadAllText(file.FullName);
        //    dynamic overlayConfig = ParseJson(overlayJson);
        //    return Merger.Merge(overlayConfig, config);
        //}

        //public static ConfigObject ApplyJsonFromPath(string path, ConfigObject config = null) {
        //    return ApplyJsonFromFileInfo(new FileInfo(path), config);
        //}

        public static ConfigObject ApplyJson(string json, ConfigObject config = null) {
            if(config == null) config = new ConfigObject();

            dynamic parsed = ParseJson(json);
            return Merger.Merge(parsed, config);
        }

        //// seeks a folder for .conf files
        //public static ConfigObject ApplyFromDirectory(string path, ConfigObject config = null, bool recursive = false) {
        //    if(!Directory.Exists(path)) throw new Exception("No folder found in the given path.");

        //    if(config == null) config = new ConfigObject();

        //    var info = new DirectoryInfo(path);
        //    if(recursive) config = info.GetDirectories().Aggregate(config, (current, dir) => ApplyFromDirectoryInfo(dir, current, true));

        //    // find all files
        //    var files = info.GetFiles();
        //    return files.Aggregate(config, (current, file) => ApplyJsonFromFileInfo(file, current));
        //}

        //public static ConfigObject ApplyFromDirectoryInfo(DirectoryInfo info, ConfigObject config = null, bool recursive = false) {
        //    return ApplyFromDirectory(info.FullName, config, recursive);
        //}

        public static ConfigObject ParseJson(string json) {
            var parsed = JsonConvert.DeserializeObject<ExpandoObject>(json, new ExpandoObjectConverter());

            // transform the ExpandoObject to the format expected by ConfigObject
            parsed = JsonNetAdapter.Transform(parsed);

            // convert the ExpandoObject to ConfigObject before returning
            var result = ConfigObject.FromExpando(parsed);
            return result;
        }

        static dynamic GetDefaultConfig(Assembly assembly) {
            var dconfJson = ScanForDefaultConfig(assembly);
            return dconfJson == null ? null : ParseJson(dconfJson);
        }

        static string ScanForDefaultConfig(Assembly assembly) {
            if(assembly == null) assembly = Assembly.GetEntryAssembly();

            string[] res;
            try {
                // this might fail for the 'Anonymously Hosted DynamicMethods Assembly' created by an Reflect.Emit()
                res = assembly.GetManifestResourceNames();
            } catch {
                // for those assemblies, we don't provide a config
                return null;
            }
            var dconfResource = res.FirstOrDefault(r => 
                r.EndsWith("Default.conf.json", StringComparison.OrdinalIgnoreCase) ||
                r.EndsWith("Default.conf", StringComparison.OrdinalIgnoreCase) ||
                r.EndsWith("Default.json", StringComparison.OrdinalIgnoreCase)
            );

            if(string.IsNullOrEmpty(dconfResource)) return null;

            var stream = assembly.GetManifestResourceStream(dconfResource);
            var defaultJson = new StreamReader(stream).ReadToEnd();
            return defaultJson;
        }
    }
}
