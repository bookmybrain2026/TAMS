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
            checkbox.checked = false;
            bubble.classList.remove("selected");
        }
    });

    if (visibleCourses === 0 && selectedSubject !== "") {
        noCoursesMessage.style.display = "block";
    }
    else {
        noCoursesMessage.style.display = "none";
    }
}

courseBubbles.forEach(function (bubble) {
    const checkbox = bubble.querySelector("input");
    if (checkbox.checked) {
        bubble.classList.add("selected");
    }

    bubble.addEventListener("click", function () {
        checkbox.checked = !checkbox.checked;
        if (checkbox.checked) {
            bubble.classList.add("selected");
        }
        else {
            bubble.classList.remove("selected");
        }
    });
});
subjectSelect.addEventListener("change", updateCourses);
updateCourses();


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

function searchAdminAppointments() {
    const input = document.getElementById("adminAppointmentSearch");

    if (!input) return;

    const filter = input.value.trim().toLowerCase();
    const appointmentRows = document.querySelectorAll(".admin-appointment-row");
    const noMatchMessage = document.getElementById("adminAppointmentNoMatch");

    let visibleCount = 0;

    appointmentRows.forEach(function (row) {
        const searchText = row.getAttribute("data-search") || "";
        const matches = searchText.toLowerCase().includes(filter);

        row.style.display = matches ? "" : "none";

        if (matches) {
            visibleCount++;
        }
    });

    if (noMatchMessage) {
        noMatchMessage.style.display =
            appointmentRows.length > 0 && visibleCount === 0 ? "" : "none";
    }
}


async function openTutorModal(url, modalId) {
    try {
        const response = await fetch(url, { credentials: "same-origin", headers: { "X-Requested-With": "XMLHttpRequest" } });

        if (!response.ok) { throw new Error("Could not retrieve tutor information."); }

        const html = await response.text();
        const doc = new DOMParser().parseFromString(html, "text/html");
        const modal = doc.getElementById(modalId);

        if (!modal) { throw new Error("Tutor popup not found."); }

        document.getElementById(modalId)?.remove();

        const newModal = document.importNode(modal, true);
        newModal.style.display = "flex";
        newModal.setAttribute("aria-hidden", "false");

        document.body.appendChild(newModal);
        document.body.style.overflow = "hidden";

        newModal.querySelector( "input:not([type='hidden']), button, select, textarea" )?.focus();

    } catch (error) {
        console.error("Tutor popup error:", error);
        alert("Could not open the tutor popup. Please refresh the page and try again.");
    }
}

function closeTutorModal(modalId) {
    document.getElementById(modalId)?.remove();

    const tutorModalOpen = !!document.querySelector(".tutor-modal");
    const otherModalIds = ["addSubjectModal", "editSubjectModal", "addCourseModal", "editCourseModal"];

    const otherModalOpen = otherModalIds.some(id => {
        const modal = document.getElementById(id);
        return modal && getComputedStyle(modal).display !== "none";
    });

    if (!tutorModalOpen && !otherModalOpen) { document.body.style.overflow = ""; }
}

document.addEventListener("click", event => {
    const modal = event.target.closest(".tutor-modal");
    if (modal && event.target === modal) { closeTutorModal(modal.id); }
});

document.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        document.querySelectorAll(".tutor-modal").forEach(modal => { closeTutorModal(modal.id); });
    }
});

document.addEventListener("DOMContentLoaded", () => {
    const modal = document.getElementById("editTutorModal") || document.getElementById("tutorScheduleModal");

    if (modal) {
        modal.style.display = "flex";
        modal.setAttribute("aria-hidden", "false");
        document.body.style.overflow = "hidden";
    }
});


