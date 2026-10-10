// Show Add Subject 
function showSubjectAddForm() {
    hideSubjectForms();

    const modal = document.getElementById("addSubjectModal");

    if (modal) {
        modal.style.display = "flex";
        document.body.style.overflow = "hidden";
    }
}

// Show Edit Subject 
function showSubjectEditForm(subjectId, code, name) {
    hideSubjectForms();

    document.getElementById("EditSubjectId").value = subjectId;
    document.getElementById("EditCode").value = code;
    document.getElementById("EditName").value = name;

    const modal = document.getElementById("editSubjectModal");

    if (modal) {
        modal.style.display = "flex";
        document.body.style.overflow = "hidden";
    }
}

// Hide Add and Edit Subject 
function hideSubjectForms() {
    const addModal = document.getElementById("addSubjectModal");
    const editModal = document.getElementById("editSubjectModal");

    if (addModal) {
        addModal.style.display = "none";
    }

    if (editModal) {
        editModal.style.display = "none";
    }

    document.body.style.overflow = "";
}

// Close Subject when clicking the dark background
document.addEventListener("click", function (event) {
    const addModal = document.getElementById("addSubjectModal");
    const editModal = document.getElementById("editSubjectModal");

    if (event.target === addModal || event.target === editModal) {
        hideSubjectForms();
    }
});

// Close Subject  with Escape
document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
        hideSubjectForms();
    }
});
function searchSubjects() {
    const searchInput = document.getElementById("subjectSearch");
    const noMatch = document.getElementById("subjectNoMatch");

    if (!searchInput) return;

    const search = searchInput.value.toLowerCase().trim();
    const rows = document.querySelectorAll(".subject-row");
    let visibleCount = 0;

    rows.forEach(row => {
        const text = (row.dataset.search || "").toLowerCase();
        const matches = text.includes(search);

        row.style.display = matches ? "" : "none";

        if (matches) visibleCount++;
    });

    if (noMatch) {
        noMatch.style.display =
            rows.length > 0 && visibleCount === 0 ? "" : "none";
    }
}


// Show Add Course 
function showCourseAddForm() {
    hideCourseForms();

    const modal = document.getElementById("addCourseModal");

    if (modal) {
        modal.style.display = "flex";
        document.body.style.overflow = "hidden";
    }
}

// Show Edit Course 
function showCourseEditForm(courseId, subjectCode, courseCode, courseName) {
    hideCourseForms();

    document.getElementById("EditCourseId").value = courseId;
    document.getElementById("EditSubjectCode").value = subjectCode;
    document.getElementById("EditCourseCode").value = courseCode;
    document.getElementById("EditCourseName").value = courseName;

    const modal = document.getElementById("editCourseModal");

    if (modal) {
        modal.style.display = "flex";
        document.body.style.overflow = "hidden";
    }
}

// Hide Add and Edit Course 
function hideCourseForms() {
    const addModal = document.getElementById("addCourseModal");
    const editModal = document.getElementById("editCourseModal");

    if (addModal) {
        addModal.style.display = "none";
    }

    if (editModal) {
        editModal.style.display = "none";
    }

    document.body.style.overflow = "";
}

// Close either by clicking its dark background
document.addEventListener("click", function (event) {
    const addModal = document.getElementById("addCourseModal");
    const editModal = document.getElementById("editCourseModal");

    if (event.target === addModal || event.target === editModal) {
        hideCourseForms();
    }
});

// Close either modal with Escape
document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
        hideCourseForms();
    }
});

function searchCourses() {
    const search = document.getElementById("courseSearch")
        .value.toLowerCase().trim();

    const rows = document.querySelectorAll(".course-row");
    const noMatch = document.getElementById("courseNoMatch");
    let visibleCount = 0;

    rows.forEach(row => {
        const text = row.dataset.search.toLowerCase();
        const matches = text.includes(search);

        row.style.display = matches ? "" : "none";

        if (matches) visibleCount++;
    });

    if (noMatch) {
        noMatch.style.display =
            rows.length > 0 && visibleCount === 0 ? "" : "none";
    }
}

function searchTutors() {
    var input = document.getElementById("tutorSearch");
    var filter = input.value.toLowerCase();

    var tutors = document.querySelectorAll(".tutor-card");

    tutors.forEach(function (tutor) {
        var text = tutor.textContent.toLowerCase();

        if (text.includes(filter)) {
            tutor.style.display = "";
        } else {
            tutor.style.display = "none";
        }
    });
}

const subjectSelect = document.getElementById("subjectSelect");

if (subjectSelect) {
    const courseBubbles = document.querySelectorAll(".course-bubble");
    const noCoursesMessage = document.getElementById("noCoursesMessage");

    function updateCourses() {
        const selectedSubject = subjectSelect.value;
        let visibleCourses = 0;

        courseBubbles.forEach(function (bubble) {
            const courseSubject = bubble.getAttribute("data-subject");
            const checkbox = bubble.querySelector("input");

            if (courseSubject === selectedSubject) {
                bubble.style.display = "inline-flex";
                visibleCourses++;
            }
            else {
                bubble.style.display = "none";
                if (checkbox) { checkbox.checked = false; }
                bubble.classList.remove("selected");
            }
        });

        if (noCoursesMessage) {
            noCoursesMessage.style.display =
                visibleCourses === 0 && selectedSubject !== "" ? "block" : "none";
        }
    }

    courseBubbles.forEach(function (bubble) {
        const checkbox = bubble.querySelector("input");
        if (!checkbox) return;
        if (checkbox.checked) { bubble.classList.add("selected"); }
        bubble.addEventListener("click", function (event) {
            if (event.target === checkbox) {
                bubble.classList.toggle("selected", checkbox.checked);
                return;
            }
            checkbox.checked = !checkbox.checked;
            bubble.classList.toggle("selected", checkbox.checked);
        });
    });
    subjectSelect.addEventListener("change", updateCourses);
    updateCourses();
}


//Student Dashboard search
function searchStudentTutors() {
    const searchInput = document
        .getElementById("tutorSearch")
        .value
        .trim()
        .toLowerCase();

    const tutorCards = document.querySelectorAll(
        "#tutorResults .tutor-result-item"
    );

    const noResultsMessage = document.getElementById(
        "noTutorsMatch"
    );

    let visibleTutors = 0;

    tutorCards.forEach(function (card) {
        const tutorInfo = card
            .getAttribute("data-search")
            .toLowerCase();

        const matches = tutorInfo.includes(searchInput);

        card.style.display = matches ? "" : "none";

        if (matches) {
            visibleTutors++;
        }
    });

    if (noResultsMessage) {
        noResultsMessage.style.display =
            visibleTutors === 0 ? "block" : "none";
    }
}

function searchAdminTutors() {
    const input = document.getElementById("adminTutorSearch");

    if (!input) return;

    const filter = input.value.trim().toLowerCase();
    const tutorRows = document.querySelectorAll(".admin-tutor-row");
    const noMatchMessage = document.getElementById("adminTutorNoMatch");

    let visibleCount = 0;

    tutorRows.forEach(function (row) {
        const searchText = row.getAttribute("data-search") || "";
        const matches = searchText.toLowerCase().includes(filter);

        row.style.display = matches ? "" : "none";

        if (matches) {
            visibleCount++;
        }
    });

    if (noMatchMessage) {
        noMatchMessage.style.display =
            tutorRows.length > 0 && visibleCount === 0 ? "" : "none";
    }
}

function searchAdminStudents() {
    const input = document.getElementById("adminStudentSearch");

    if (!input) return;

    const filter = input.value.trim().toLowerCase();
    const studentRows = document.querySelectorAll(".admin-student-row");
    const noMatchMessage = document.getElementById("adminStudentNoMatch");

    let visibleCount = 0;

    studentRows.forEach(function (row) {
        const searchText = row.getAttribute("data-search") || "";
        const matches = searchText.toLowerCase().includes(filter);

        row.style.display = matches ? "" : "none";

        if (matches) {
            visibleCount++;
        }
    });

    if (noMatchMessage) {
        noMatchMessage.style.display =
            studentRows.length > 0 && visibleCount === 0 ? "" : "none";
    }
}


async function openTutorModal(url, modalId) {
    try {
        const response = await fetch(url, {
            method: "GET",
            credentials: "same-origin",
            headers: {
                "X-Requested-With": "XMLHttpRequest"
            }
        });

        if (!response.ok) { throw new Error("Could not retrieve the tutor information."); }
        const html = await response.text();
        const parsedDocument = new DOMParser().parseFromString(html,
            "text/html"
        );

        const modal = parsedDocument.getElementById(modalId);

        if (!modal) {
            throw new Error("The requested popup was not found. Check the tutor ID and login session.");
        }
        const existingModal = document.getElementById(modalId);
        if (existingModal) { existingModal.remove(); }

        const currentModal = document.importNode(modal, true);

        currentModal.style.display = "flex";
        currentModal.setAttribute("aria-hidden", "false");

        document.body.appendChild(currentModal);
        document.body.style.overflow = "hidden";

        // Put keyboard focus inside the popup.
        const firstInput = currentModal.querySelector(
            "input:not([type='hidden']), button, select, textarea"
        );

        if (firstInput) {
            firstInput.focus();
        }
    } catch (error) {
        console.error("Tutor popup error:", error);
        alert("Could not open the tutor popup. Please refresh the page and try again.");
    }
}

function closeTutorModal(modalId) {
    const modal = document.getElementById(modalId);
    if (modal) {  modal.remove(); }
    const tutorModalOpen = document.querySelector(".tutor-modal") !== null;
    const otherModalIds = ["addSubjectModal", "editSubjectModal", "addCourseModal", "editCourseModal"];
    const otherModalOpen = otherModalIds.some(function (id) {
        const element = document.getElementById(id);
        return element && window.getComputedStyle(element).display !== "none";
    });
    if (!tutorModalOpen && !otherModalOpen) {
        document.body.style.overflow = "";
    }
}

document.addEventListener("click", function (event) {
    const modal = event.target.closest(".tutor-modal");
    if (modal && event.target === modal) {
        closeTutorModal(modal.id);
    }
});

document.addEventListener("keydown", function (event) {
    if (event.key === "Escape") {
        document.querySelectorAll(".tutor-modal").forEach(function (modal) {
            closeTutorModal(modal.id);
        });
    }
});

document.addEventListener("DOMContentLoaded", function () {
    const editModal = document.getElementById("editTutorModal");
    const scheduleModal = document.getElementById("tutorScheduleModal");

    if (editModal) {
        editModal.style.display = "flex";
        editModal.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";
    } else if (scheduleModal) {
        scheduleModal.style.display = "flex";
        scheduleModal.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";
    }
});