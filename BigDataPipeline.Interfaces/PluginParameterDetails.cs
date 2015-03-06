using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BigDataPipeline.Interfaces
{
    public class ModuleParameterDetails
    {
        public string Name { get; set; }
        
        public string Type { get; set; }
        
        public string Description { get; set; }

        //public string Default { get; set; }
        
        public List<string> Options { get; set; }

        public bool IsRequired { get; set; }

        public ModuleParameterDetails ()
        {
        }

        public ModuleParameterDetails (string name, string type, string description, bool isRequired = false)
        {
            Name = name;
            Type = type;
            Description = description;
            IsRequired = isRequired;
            SetDefaultTypeOptions ();
        }

        public ModuleParameterDetails (string name, Type type, string description, bool required = false) :
            this (name, type.Name, description, required)
        {            
        }

        public ModuleParameterDetails SetOptions (params string[] values)
        {
            Options = new List<string> (values);
            return this;
        }

        private void SetDefaultTypeOptions ()
        {
            if (Type == null)
                return;
            switch (Type.ToLowerInvariant ())
            {
                case "boolean":
                case "bool":
                    {
                        SetOptions ("true", "false");
                    }
                    break;
            }
        }
    }
}
