// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;

namespace System.CodeDom
{
    public class CodeTypeMemberCollection : CollectionBase
    {
        public CodeTypeMemberCollection() { }

        public CodeTypeMemberCollection(CodeTypeMemberCollection value)
        {
            AddRange(value);
        }

        public CodeTypeMemberCollection(CodeTypeMember[] value)
        {
            AddRange(value);
        }

        public CodeTypeMember this[int index]
        {
            get => (CodeTypeMember)List[index];
            set => List[index] = value;
        }

        public int Add(CodeTypeMember value) => List.Add(value);

        public void AddRange(CodeTypeMember[] value)
        {
            ArgumentNullException.ThrowIfNull(value);

            for (int i = 0; i < value.Length; i++)
            {
                Add(value[i]);
            }
        }

        public void AddRange(CodeTypeMemberCollection value)
        {
            ArgumentNullException.ThrowIfNull(value);

            int currentCount = value.Count;
            for (int i = 0; i < currentCount; i++)
            {
                Add(value[i]);
            }
        }

        public bool Contains(CodeTypeMember value) => List.Contains(value);

        public void CopyTo(CodeTypeMember[] array, int index) => List.CopyTo(array, index);

        public int IndexOf(CodeTypeMember value) => List.IndexOf(value);

        public void Insert(int index, CodeTypeMember value) => List.Insert(index, value);

        public void Remove(CodeTypeMember value) => List.Remove(value);
    }
}
