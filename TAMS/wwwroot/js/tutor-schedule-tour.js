document.addEventListener("DOMContentLoaded", function () {
    const config = window.scheduleTourConfig;

    if (!config || !config.showTour) return;

    const steps = [
        {
            element: ".schedule-heading",
            popover: {
                title: "Welcome to Your Schedule",
                description: "Manage your tutoring availability and create group appointments."
            }
        },
        {
            element: ".schedule-button-group",
            popover: {
                title: "Create Group Appointment",
                description: "Create a tutoring session that multiple students can join."
            }
        },
        {
            element: ".schedule-toolbar",
            popover: {
                title: "Navigate Months",
                description: "Use Previous Month and Next Month to navigate your calendar."
            }
        },
        {
            element: ".schedule-legend",
            popover: {
                title: "Schedule Colors",
                description: "Understand the colors used for availability and group appointments."
            }
        },
        {
            element: ".schedule-table-wrapper",
            popover: {
                title: "Monthly Calendar",
                description: "View your available tutoring slots for the selected month."
            }
        },
        {
            element: ".schedule-add-form",
            popover: {
                title: "Add Availability",
                description: "Select start and end times, then click Add Schedule."
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
                    "Content-Type": "application/x-www-form-urlencoded",
                    "RequestVerificationToken": token
                },
                credentials: "same-origin",
                body: new URLSearchParams({
                    tourType: config.tourType
                })
            });

            if (!response.ok) {
                throw new Error("Could not save tour completion.");
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
