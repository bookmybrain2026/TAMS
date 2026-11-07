document.addEventListener("DOMContentLoaded", function () {
    const config = window.dashboardTourConfig;

    if (!config || !config.showTour) return;

    const tours = {
        Student: [
            {
                element: ".student-notification-button",
                popover: {
                    title: "Notification Bell",
                    description: "View important updates, appointment reminders, and notifications about your appointments."
                }
            },
            {
                element: ".profile-card",
                popover: {
                    title: "Your Profile",
                    description: "View your personal information."
                }
            },
            {
                element: ".appointments-card",
                popover: {
                    title: "Your Appointments",
                    description: "View and manage your tutoring appointments."
                }
            },
            {
                element: ".student-groups-card",
                popover: {
                    title: "Group Appointments",
                    description: "See your group tutoring sessions."
                }
            },
            {
                element: ".tutors-card",
                popover: {
                    title: "Find a Tutor",
                    description: "Explore available tutors."
                }
            },
            {
                element: ".booking-card",
                popover: {
                    title: "Book an Appointment",
                    description: "Schedule a tutoring session."
                }
            }
        ],

        Tutor: [
            {
                element: ".tutor-notification-button",
                popover: {
                    title: "Notifications",
                    description: "View important updates, appointment reminders, and notifications about your appointments."
                }
            },
            {
                element: ".tutor-profile-panel",
                popover: {
                    title: "Your Profile",
                    description: "View your profile information, subject, ratings, and student reviews."
                }
            },
            {
                element: ".tutor-stats-panel",
                popover: {
                    title: "Class Overview",
                    description: "Monitor upcoming, completed, and total tutoring appointments."
                }
            },
            {
                element: ".tutor-quick-panel",
                popover: {
                    title: "Quick Actions",
                    description: "Manage your schedule, view appointments, edit your profile, and download reports."
                }
            },
            {
                element: ".tutor-upcoming-panel",
                popover: {
                    title: "Upcoming Appointments",
                    description: "View upcoming tutoring sessions, complete appointments, or cancel them."
                }
            },
            {
                element: ".tutor-focus-panel",
                popover: {
                    title: "Your Schedule",
                    description: "See your next tutoring sessions and available schedule entries."
                }
            },
            {
                element: ".tutor-groups-panel",
                popover: {
                    title: "Group Appointments",
                    description: "View your group sessions, enrollment capacity, and appointment details."
                }
            },
            {
                element: ".tutor-history-panel",
                popover: {
                    title: "Appointment History",
                    description: "Review previous appointments and their statuses."
                }
            },
            {
                element: ".tutor-schedule-panel",
                popover: {
                    title: "Manage Availability",
                    description: "View and manage the time slots students can book."
                }
            },
            {
                element: ".tutor-reviews-panel",
                popover: {
                    title: "Student Reviews",
                    description: "Read student feedback and see your overall tutoring rating."
                }
            }
        ],

        Administrator: [
            {
                element: ".admin-notification-button",
                popover: {
                    title: "Notifications",
                    description: "Stay updated on appointment bookings, cancellations, group sessions, and other important announcements."
                }
            },
            {
                element: ".dashboard-header-actions",
                popover: {
                    title: "Account Management",
                    description: "Create new tutor and administrator accounts."
                }
            },
            {
                element: ".dashboard-summary",
                popover: {
                    title: "System Overview",
                    description: "View the total number of registered tutors, students, and appointments."
                }
            },            
            {
                element: ".management-grid",
                popover: {
                    title: "System Management",
                    description: "Manage subjects, courses, and appointment scheduling."
                }
            },
            {
                element: "#adminTutorSearch",
                popover: {
                    title: "Tutor Directory",
                    description: "Search for tutors, edit their information, view schedules, and review student feedback."
                }
            },
            {
                element: "#adminStudentSearch",
                popover: {
                    title: "Student Directory",
                    description: "Search for registered students and manage their accounts."
                }
            },
            {
                element: "#adminAppointmentSearch",
                popover: {
                    title: "Appointment Management",
                    description: "Monitor scheduled, completed, and cancelled tutoring appointments."
                }
            },
            {
                element: ".admin-groups-section",
                popover: {
                    title: "Group Appointments",
                    description: "Monitor approved group sessions, enrollment capacity, and appointment statuses."
                }
            },
            {
                element: ".analytics-panel",
                popover: {
                    title: "Reports and Exports",
                    description: "Download appointment reports, tutor schedules, user accounts, and system activity."
                }
            }
        ]

    };

    const steps = (tours[config.role] || []).filter(step => document.querySelector(step.element));
    if (steps.length === 0) return;

    let saving = false;

    async function completeTour() {
        if (saving) return;

        saving = true;

        try {
            const response = await fetch(config.completeUrl, {
                method: "POST",
                headers: {
                    "RequestVerificationToken": config.token
                },
                credentials: "same-origin"
            });

            if (!response.ok)
                throw new Error("Could not save tour completion.");

            tour.destroy();

        } catch (error) {
            saving = false;
            console.error(error);
            alert("Could not save your tour progress. Please try again.");
        }
    }

    const tour = window.driver.js.driver({
        popoverClass: "tams-tour",
        showProgress: true,
        animate: true,
        allowClose: false,
        steps: steps,

        nextBtnText: "Next",
        prevBtnText: "Back",
        doneBtnText: "Finish",

        onNextClick: () => {
            if (tour.isLastStep()) {
                completeTour();
            } else {
                tour.moveNext();
            }
        },

        onPrevClick: () => {
            tour.movePrevious();
        },

        onPopoverRender: (popover) => {
            const skipButton = document.createElement("button");

            skipButton.type = "button";
            skipButton.textContent = "Skip Tour";
            skipButton.className = "tour-skip-button";

            skipButton.addEventListener("click", completeTour);

            popover.footerButtons.prepend(skipButton);
        }
    });

    tour.drive();
});
