﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Data;

namespace Bapers.GUI.officeManager
{
    /// <summary>
    /// Interaction logic for addTasks.xaml
    /// </summary>
    public partial class addTasks : Window
    {
        DatabaseConnector db = new DatabaseConnector();
        string selectedTask = "";

        public addTasks()
        {
            InitializeComponent();
            populate();
        }
        private async void populate()
        {
             await db.Select(taskGrid, "SELECT task_id, task_description, location, task_duration, price FROM Tasks;");
        }


        private async void add_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await db.InQuery("INSERT INTO Tasks (task_description,location,task_duration, price,amount)VALUES(@val0, @val1, @val2,@val3,@val4)", description_txtBox.Text, location_txtBox.Text, duration_txtBox.Text, price_txtBox.Text,0);
                MessageBox.Show("Successfully added task");
                description_txtBox.Clear();
                location_txtBox.Clear();
                duration_txtBox.Clear();
                price_txtBox.Clear();
            }
            catch (Exception)
            {
                MessageBox.Show("Failed to add task");
                throw;
            }
        }

        private void back_Click(object sender, RoutedEventArgs e)
        {
            officeManagerPortal officeManagerWindow = new officeManagerPortal();
            officeManagerWindow.Show();
            this.Close();
        }

        private void logOut_Click(object sender, RoutedEventArgs e)
        {
            Login loginWindow = new Login();
            loginWindow.Show();
            this.Close();
        }
 
        private void searchChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void ontaskChange(object sender, SelectedCellsChangedEventArgs e)
        {
            foreach (var item in e.AddedCells)
            {
                if (item.Column != null)
                {
                    string col = item.Column.Header.ToString();
                    //tmp job num to be able to create the map value

                    //assuming job num always appears before the price
                    if (col.Equals("task_id") && item.Column.GetCellContent(item.Item) != null)
                    {
                        selectedTask = (item.Column.GetCellContent(item.Item) as TextBlock).Text;
                    }
                }
            }
        }

        private async void save_Click(object sender, RoutedEventArgs e)
        {
            //save the account details if changed
            taskGrid.CommitEdit();
            foreach (System.Data.DataRowView dr in taskGrid.ItemsSource)
            {
                await db.InQuery(
                    "UPDATE Tasks " +
                    "SET task_description = @val0, " +
                    "location = @val1, " +
                    "task_duration = @val2, " +
                    "price = @val3 " +
                    "WHERE task_ID = @val4; "
                    , dr.Row.Field<string>("task_description")
                    , dr.Row.Field<string>("location")
                    , dr.Row.Field<float>("task_duration")
                    , dr.Row.Field<float>("price")
                    , dr.Row.Field<int>("task_id")
                );
                //account_number, first_name, last_name, phone_number, address, company_name
            }
            MessageBox.Show("Tasks updated");
        }

        private async void delete_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTask.Equals(""))
            {
                MessageBox.Show("No task selected");
                return;
            }

            if (!selectedTask.Equals(""))
            {
                MessageBoxResult confirmResult = MessageBox.Show("Are you sure you want to delete the Task?", "Confirm Delete", MessageBoxButton.YesNo);
                if (confirmResult == MessageBoxResult.No)
                    return;
            }

            //all remove all job tasks with this task id
            await db.InQuery("DELETE FROM Job_tasks WHERE Taskstask_ID = @val0;", int.Parse(selectedTask) );
            //remove the task
            await db.InQuery("DELETE FROM Tasks WHERE task_id = @val0;", int.Parse(selectedTask) );

            populate();

        }
    }
}
