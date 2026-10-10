// Show Add Subject form
function showSubjectAddForm() {
    hideSubjectForms();
    document.getElementById("addForm").style.display = "block";
}

// Show Edit Subject form
function showSubjectEditForm(subjectId, code, name) {
    hideSubjectForms();

    document.getElementById("EditSubjectId").value = subjectId;
    document.getElementById("EditCode").value = code;
    document.getElementById("EditName").value = name;

    document.getElementById("editForm").style.display = "block";
}

// Hide Subject forms
function hideSubjectForms() {
    document.getElementById("addForm").style.display = "none";
    document.getElementById("editForm").style.display = "none";
}

// Search Subjects
function searchSubjects() {
    var input = document.getElementById("subjectSearch");
    var filter = input.value.toLowerCase();

    var table = document.getElementById("subjectTableBody");
    var rows = table.getElementsByTagName("tr");

    for (var i = 0; i < rows.length; i++) {
        var code = rows[i].cells[0].textContent.toLowerCase();
        var name = rows[i].cells[1].textContent.toLowerCase();

        if (code.includes(filter) || name.includes(filter)) {
            rows[i].style.display = "";
        }
        else {
            rows[i].style.display = "none";
        }
    }
}

// Show Add Course form
function showCourseAddForm() {
    hideCourseForms();
    document.getElementById("addForm").style.display = "block";
}

// Show Edit Course form
function showCourseEditForm(courseId, subjectCode, courseCode, courseName) {
    hideCourseForms();

    document.getElementById("EditCourseId").value = courseId;
    document.getElementById("EditSubjectCode").value = subjectCode;
    document.getElementById("EditCourseCode").value = courseCode;
    document.getElementById("EditCourseName").value = courseName;

    document.getElementById("editForm").style.display = "block";
}

// Hide Course forms
function hideCourseForms() {
    document.getElementById("addForm").style.display = "none";
    document.getElementById("editForm").style.display = "none";
}

// Search Courses
function searchCourses() {
    var input = document.getElementById("courseSearch");
    var filter = input.value.toLowerCase();

    var table = document.getElementById("courseTableBody");
    var rows = table.getElementsByTagName("tr");

    for (var i = 0; i < rows.length; i++) {
        var subject = rows[i].cells[0].textContent.toLowerCase();
        var courseCode = rows[i].cells[1].textContent.toLowerCase();
        var courseName = rows[i].cells[2].textContent.toLowerCase();

        if (subject.includes(filter) ||
            courseCode.includes(filter) ||
            courseName.includes(filter)) {
            rows[i].style.display = "";
        }
        else {
            rows[i].style.display = "none";
        }
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
            checkbox.checked = false; // Unselect courses that don't belong to the selected subject.
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


// Change bubble appearance when selected.
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
// Update courses when subject changes.
subjectSelect.addEventListener("change", updateCourses);

// Run when page loads.
updateCourses();


//Student Dashboard search
function searchTutors() {
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