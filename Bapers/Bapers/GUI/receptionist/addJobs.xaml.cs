﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;


namespace Bapers.GUI
{
    /// <summary>
    /// Interaction logic for addJobs.xaml
    /// </summary>
    public partial class addJobs : Window
    {
        DatabaseConnector db = new DatabaseConnector();
        List<string> list = new List<string>();

        public addJobs()
        {
            InitializeComponent();
            populate();

            //assuming the page is created after searching
            addJobFor.Text += " " + myVariables.currfname + " " + myVariables.currlname; 
        }

        private async void populate()
        {
            await db.SelectLists(list, "SELECT task_description FROM Tasks;");
            if (list != null)
            {
                foreach (string s in list)
                {
                    CheckBox cb = new CheckBox();
                    cb.Content = s;
                    cb.Width = tasks_dropDown.Width;
                    tasks_dropDown.Items.Add(cb);
                }
            }
        }

        private void back_Click(object sender, RoutedEventArgs e)
        {
            receptionist receptionistwindow = new receptionist();
            receptionistwindow.Show();
            this.Close();
        }

        private void logOut_Click(object sender, RoutedEventArgs e)
        {
            Login loginWindow = new Login();
            loginWindow.Show();
            this.Close();
        }

        private async void addJob_Click(object sender, RoutedEventArgs e)
        {
            List<string> selectedList = new List<string>();

            //get the Selected tasks
            for (int i = 1; i < tasks_dropDown.Items.Count; i++)
            {
                CheckBox cb = (tasks_dropDown.Items[i] as CheckBox);
                if ((bool)cb.IsChecked)
                    selectedList.Add(cb.Content.ToString());
            }

            if (selectedList.Count.Equals(0) || deadline_date.SelectedDate.Value.Equals(null) || time_dropDown.SelectedIndex.Equals(0))
            {
                MessageBox.Show("Please fill in all required fields");
                return;
            }

            //creates the auto incrementing value for the id
            string jobNum = await db.SelectSingle("SELECT MAX(job_number) FROM job;");
            int num = (int.Parse(jobNum.Substring(1))) + 1;

            //format the deadline
            DateTime selectedDate = new DateTime(
                deadline_date.SelectedDate.Value.Year, deadline_date.SelectedDate.Value.Month, deadline_date.SelectedDate.Value.Day
                ,int.Parse(time_dropDown.Text.Substring(0, 2)), 00, 00);

            //timespans to determine the urgency
            double minsToComplete = 0f;
            double timeTillDeadline = 0f;

            var tasksCost = new Dictionary<string,float>(); ;

            foreach(string s in selectedList)
            {
                //get the time to complete all tasks
                var val = await db.SelectSingle("SELECT task_duration FROM Tasks WHERE task_description = @val0;", s);
                minsToComplete += int.Parse(val);

                //get the cost of each task
                var val1 = await db.SelectSingle("SELECT task_id FROM Tasks WHERE task_description = @val0;", s);
                var val2 = await db.SelectSingle("SELECT price FROM Tasks WHERE task_description = @val0;", s);
                tasksCost.Add(val1, float.Parse(val2));
            }
            
            //determine if job deadline is less than 6 hour
            timeTillDeadline = (selectedDate - DateTime.Now).TotalMinutes;
            string urgency = "Normal";
            if (timeTillDeadline - minsToComplete < 360 )
                urgency = "Urgent";

            var tmpList = tasksCost;

            float totalPrice = 0f;
            switch (await db.SelectSingle(
                "SELECT discount_plan " +
                "FROM discount " +
                "WHERE Customeraccount_number = @val0; "
                , myVariables.currID)
                )
            {
                case "Fixed":
                    //removes the amount from the total cost
                    foreach (float f in tasksCost.Values)
                        totalPrice += f;
                    string val = await db.SelectSingle("SELECT discount_rate FROM fixed_discount WHERE DiscountCustomeraccount_number = @val0", myVariables.currID);
                    totalPrice -= (totalPrice * (float.Parse(val)/100));
                    break;
                case "Variable":
                    //grabs the discount and removes it from each price
                    foreach (KeyValuePair<string, float> p in tmpList.ToList())
                    {
                        string val1 = await db.SelectSingle("SELECT discount_rate FROM variable_discount WHERE DiscountCustomeraccount_number = @val0 AND task_type = @val1", myVariables.currID, p.Key);
                        tasksCost[p.Key] -= p.Value * (float.Parse(val1)/100); 
                    }
                    //takes all the discounted prices and adds them together
                    foreach(float f in tasksCost.Values)
                        totalPrice += f;
                    break;
                case "Flexible":
                    //grabs the month range from today
                    var todays_date = DateTime.Now.Date;
                    var lastmonth_date = todays_date.Subtract(TimeSpan.FromDays(30));
                    //grabs the money spent by the customer in the last 30 
                    float monthlyTotal = float.Parse(await db.SelectSingle(
                        "SELECT SUM(payment_amount) " +
                        "FROM payment " +
                        "WHERE Customeraccount_number = @val0 " +
                        "AND AND Customeraccountphone_number = @val1 " +
                        "AND payment_date BETWEEN  @val2 AND @val3; "
                        , myVariables.currID, myVariables.currnum, lastmonth_date, todays_date));
                    //grab the discount rate based on the monthly discount
                    float rate = float.Parse(await db.SelectSingle(
                        "SELECT discount_rate " +
                        "FROM flexible_discount " +
                        "WHERE DiscountCustomeraccount_number = @val0 " +
                        "AND @val1 BETWEEN  lower AND upper; "
                        , myVariables.currID, lastmonth_date, todays_date));
                    //uses the rate gotten to apply the discount to the total price
                    foreach (float f in tasksCost.Values)
                            totalPrice += f;
                    totalPrice -= (totalPrice * (rate/100));
                    break;
                default:
                    foreach (float f in tasksCost.Values)
                        totalPrice += f;
                    break;
            }

            
            //create entry in job table
            await db.InQuery(
            "INSERT INTO Job(job_Number, job_priority, deadline, job_status, special_instructions, job_completed, Customeraccount_number, Customerphone_number, discounted_total) " +
            "VALUES(@val0, @val1 , @val2, @val3, @val4, @val5, @val6, @val7, @val8);"
            ,"J" + num , urgency, selectedDate, "uncompleted", specialIn_txtBox.Text, null, myVariables.currID, myVariables.currnum, totalPrice);

            //create entries in job_tasks table
            foreach (string s in selectedList)
            {
                var taskid = await db.SelectSingle("SELECT Task_ID FROM Tasks WHERE task_description = @val0;", s);
                await db.InQuery("INSERT INTO Job_Tasks VALUES (@val0, @val1, @val2, @val3, @val4 )", "J" + num, taskid, null, null, null);
            }

            //populate the table on the page
            await db.Select(jobsGrid,
                "SELECT job_number, job_priority, deadline, job_status, special_instructions " +
                "FROM Job " +
                "WHERE job_number = @val0 " +
                "UNION  " +
                "SELECT coalesce(NULL, '*'), coalesce(NULL, 'Task'), coalesce(NULL, 'Description'), coalesce(NULL, 'Price(£)'), coalesce(NULL, ' ') " +
                "UNION " +
                "SELECT coalesce(NULL, '*'), Taskstask_id, task_description, price, coalesce(NULL, ' ') " +
                "FROM Job_Tasks, tasks " +
                "WHERE Jobjob_number = @val0 " +
                "AND Taskstask_id = task_id; "
                , "J" + num);
            

            System.Windows.Forms.MessageBox.Show("Job has been added successfully");
            
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void onChange(object sender, SelectionChangedEventArgs e)
        {
            tasks_dropDown.SelectedIndex = 0;
        }
    }
}
