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
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace JsonConfig {

    public class ConfigObject: DynamicObject, IDictionary<string, object> {
        Dictionary<string, object> _members = new Dictionary<string, object>();

        #region IEnumerable implementation

        public IEnumerator GetEnumerator() {
            return _members.GetEnumerator();
        }

        #endregion

        #region IEnumerable implementation

        IEnumerator<KeyValuePair<string, object>> IEnumerable<KeyValuePair<string, object>>.GetEnumerator() {
            return _members.GetEnumerator();
        }

        #endregion

        public static ConfigObject FromExpando(ExpandoObject e) {
            var edict = e as IDictionary<string, object>;
            var c = new ConfigObject();
            var cdict = (IDictionary<string, object>)c;

            // this is not complete. It will, however work for JsonFX ExpandoObjects
            // which consists only of primitive types, ExpandoObject or ExpandoObject [] 
            // but won't work for generic ExpandoObjects which might include collections etc.
            foreach(var kvp in edict) // recursively convert and add ExpandoObjects
                switch(kvp.Value) {
                    case ExpandoObject o:
                        cdict.Add(kvp.Key, FromExpando(o));
                        break;
                    case ExpandoObject[] _:
                        cdict.Add(kvp.Key, (from ex in (ExpandoObject[])kvp.Value select FromExpando(ex)).ToArray());
                        break;
                    default:
                        cdict.Add(kvp.Key, kvp.Value);
                        break;
                }
            return c;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            result = _members.ContainsKey(binder.Name) ? _members[binder.Name] : new NullExceptionPreventer();
            return true;
        }

        public override bool TrySetMember(SetMemberBinder binder, object value) {
            if(_members.ContainsKey(binder.Name)) {
                _members[binder.Name] = value;
            } else {
                _members.Add(binder.Name, value);
            }
            return true;
        }

        public override bool TryInvokeMember(InvokeMemberBinder binder, object[] args, out object result) {
            // some special methods that should be in our dynamic object
            switch(binder.Name) {
                //case "ApplyJsonFromFile" when args.Length == 1 && args[0] is string:
                //    result = Config.ApplyJsonFromFileInfo(new FileInfo((string)args[0]), this);
                //    return true;
                //case "ApplyJsonFromFile" when args.Length == 1 && args[0] is FileInfo:
                //    result = Config.ApplyJsonFromFileInfo((FileInfo)args[0], this);
                //    return true;
                case "Clone":
                    result = Clone();
                    return true;
                case "Exists" when args.Length == 1 && args[0] is string:
                    result = _members.ContainsKey((string)args[0]);
                    return true;
                default:
                    // no other methods available, error
                    result = null;
                    return false;
            }
        }

        public override string ToString() {
            return JsonConvert.SerializeObject(_members);
        }

        public void ApplyJson(string json) {
            var result = Config.ApplyJson(json, this);
            // replace myself's members with the new ones
            _members = result._members;
        }

        public static implicit operator ConfigObject(ExpandoObject exp) {
            return FromExpando(exp);
        }

        #region casts

        public static implicit operator bool(ConfigObject c) {
            // we want to test for a member:
            // if (config.SomeMember) { ... }
            //
            // instead of:
            // if (config.SomeMember != null) { ... }

            // we return always true, because a NullExceptionPreventer is returned when member
            // does not exist
            return true;
        }

        #endregion

        #region ICollection implementation

        public void Add(KeyValuePair<string, object> item) {
            _members.Add(item.Key, item.Value);
        }

        public void Clear() {
            _members.Clear();
        }

        public bool Contains(KeyValuePair<string, object> item) {
            return _members.ContainsKey(item.Key) && _members[item.Key] == item.Value;
        }

        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) {
            throw new NotImplementedException();
        }

        public bool Remove(KeyValuePair<string, object> item) {
            throw new NotImplementedException();
        }

        public int Count => _members.Count;

        public bool IsReadOnly => throw new NotImplementedException();

        #endregion

        #region IDictionary implementation

        public void Add(string key, object value) {
            _members.Add(key, value);
        }

        public bool ContainsKey(string key) {
            return _members.ContainsKey(key);
        }

        public bool Remove(string key) {
            return _members.Remove(key);
        }

        public object this[string key] {
            get => _members[key];
            set => _members[key] = value;
        }

        public ICollection<string> Keys => _members.Keys;

        public ICollection<object> Values => _members.Values;

        public bool TryGetValue(string key, out object value) {
            return _members.TryGetValue(key, out value);
        }

        #region ICloneable implementation

        object Clone() {
            return Merger.Merge(new ConfigObject(), this);
        }

        #endregion

        #endregion
    }

    /// <summary>
    ///     Null exception preventer. This allows for hassle-free usage of configuration values that are not
    ///     defined in the config file. I.e. we can do Config.Scope.This.Field.Does.Not.Exist.Ever, and it will
    ///     not throw an NullPointer exception, but return te NullExceptionPreventer object instead.
    ///     The NullExceptionPreventer can be cast to everything, and will then return default/empty value of
    ///     that datatype.
    /// </summary>
    public class NullExceptionPreventer: DynamicObject {

        // all member access to a NullExceptionPreventer will return a new NullExceptionPreventer
        // this allows for infinite nesting levels: var s = Obj1.foo.bar.bla.blubb; is perfectly valid
        public override bool TryGetMember(GetMemberBinder binder, out object result) {
            result = new NullExceptionPreventer();
            return true;
        }

        // Add all kinds of datatypes we can cast it to, and return default values
        // cast to string will be null
        public static implicit operator string(NullExceptionPreventer nep) {
            return null;
        }

        public override string ToString() {
            return null;
        }

        public static implicit operator string[] (NullExceptionPreventer nep) {
            return new string[] { };
        }

        // cast to bool will always be false
        public static implicit operator bool(NullExceptionPreventer nep) {
            return false;
        }

        public static implicit operator bool[] (NullExceptionPreventer nep) {
            return new bool[] { };
        }

        public static implicit operator int[] (NullExceptionPreventer nep) {
            return new int[] { };
        }

        public static implicit operator long[] (NullExceptionPreventer nep) {
            return new long[] { };
        }

        public static implicit operator int(NullExceptionPreventer nep) {
            return 0;
        }

        public static implicit operator long(NullExceptionPreventer nep) {
            return 0;
        }

        // nullable types always return null
        public static implicit operator bool? (NullExceptionPreventer nep) {
            return null;
        }

        public static implicit operator int? (NullExceptionPreventer nep) {
            return null;
        }

        public static implicit operator long? (NullExceptionPreventer nep) {
            return null;
        }
    }
}
