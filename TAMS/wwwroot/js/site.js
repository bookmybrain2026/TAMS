// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// Show Add Subject form
function showAddForm() {
    hideForms();
    document.getElementById("addForm").style.display = "block";
}
// Show Edit Subject form
function showEditForm(subjectId, code, name) {
    hideForms();
    document.getElementById("EditSubjectId").value = subjectId;
    document.getElementById("EditCode").value = code;
    document.getElementById("EditName").value = name;
    document.getElementById("editForm").style.display = "block";
}
// Hide all forms
function hideForms() {
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
function showAddForm() {
    hideForms();

    document.getElementById("addForm").style.display = "block";
}
// Show Edit Course form
function showEditForm(courseId, subjectCode, courseCode, courseName) {
    hideForms();
    document.getElementById("EditCourseId").value = courseId;
    document.getElementById("EditSubjectCode").value = subjectCode;
    document.getElementById("EditCourseCode").value = courseCode;
    document.getElementById("EditCourseName").value = courseName;
    document.getElementById("editForm").style.display = "block";
}
// Hide all forms
function hideForms() {
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