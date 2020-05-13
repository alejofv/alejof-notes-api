#nullable enable

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Newtonsoft.Json;

namespace Alejof.Notes.Extensions
{
    public static class StringBuilderExtensions
    {
        public static StringBuilder AppendItems<TItem>(this StringBuilder stringBuilder, IEnumerable<TItem> items, Func<TItem, string> valueSelector)
        {
            foreach (var item in items)
                stringBuilder.AppendLine(valueSelector(item));
            
            return stringBuilder;
        }
    }
}
