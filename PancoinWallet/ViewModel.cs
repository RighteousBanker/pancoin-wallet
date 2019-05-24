using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace PancoinWallet
{
    public class ViewModel
    {
        public string Version { get; set; }

        public bool IsAccessible { get; set; }

        public string Hostname { get; set; }

        public object Data { get; set; }

        public ViewModel(object data)
        {
            Version = "0.1";
            IsAccessible = false;
            Hostname = "";
            Data = data;
        }
    }

    public class ViewModel<T>
    {
        public string Version { get; set; }

        public bool IsAccessible { get; set; }

        public string Hostname { get; set; }

        public T Data { get; set; }

        public ViewModel(T data)
        {
            Version = "0.1";
            IsAccessible = false;
            Hostname = "";
            Data = data;
        }
    }
}
