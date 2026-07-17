using System.Collections.ObjectModel;

namespace kicquwp
{
    /// <summary>
    /// Группа контактов для сгруппированного ListView
    /// </summary>
    public class ContactGroup : ObservableCollection<Contact>
    {
        public string GroupName { get; private set; }

        public ContactGroup(string name, ObservableCollection<Contact> contacts)
            : base(contacts)
        {
            GroupName = name;
        }
    }
}