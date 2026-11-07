
document.addEventListener("DOMContentLoaded", function () {
    const config = window.adminScheduleTourConfig;

    if (!config || !config.showTour) return;

    const steps = [
        {
            element: ".admin-scheduler-heading",
            popover: {
                title: "Welcome to Appointment Scheduler",
                description: "Manage tutor availability and review appointment time slots."
            }
        },
        {
            element: ".admin-scheduler-toolbar",
            popover: {
                title: "Navigate Between Months",
                description: "Use Previous Month and Next Month to view different months."
            }
        },
        {
            element: ".admin-scheduler-legend",
            popover: {
                title: "Schedule Status Colors",
                description: "Understand the colors for available, booked, completed, and expired time slots."
            }
        },
        {
            element: ".admin-scheduler-table-wrapper",
            popover: {
                title: "Monthly Schedule Calendar",
                description: "View tutor schedules and appointment statuses throughout the month."
            }
        },
        {
            element: ".admin-scheduler-slots",
            popover: {
                title: "Tutor Availability",
                description: "View available, booked, and completed time slots for each date."
            }
        },
        {
            element: '.admin-scheduler-add-form select[name="tutorId"]',
            popover: {
                title: "Select a Tutor",
                description: "Choose the tutor whose availability you want to add."
            }
        },
        {
            element: ".admin-scheduler-add-form",
            popover: {
                title: "Add a Schedule Slot",
                description: "Select the tutor and start and end times, then click Add Slot."
            }
        }
    ];

    const availableSteps = steps.filter(step =>
        document.querySelector(step.element)
    );

    if (availableSteps.length === 0) return;

    let saving = false;

    async function completeTour() {
        if (saving) return;

        saving = true;

        const token = document.querySelector(
            'input[name="__RequestVerificationToken"]'
        )?.value;

        try {
            if (!token) {
                throw new Error("Antiforgery token is missing.");
            }

            const response = await fetch(config.completeUrl, {
                method: "POST",
                headers: {
                    "RequestVerificationToken": token
                },
                credentials: "same-origin"
            });

            if (!response.ok) {
                throw new Error(
                    "Could not save Administrator Scheduler tour completion."
                );
            }

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
        steps: availableSteps,

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
