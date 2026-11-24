$(".focusOnEnter").on("keydown", function (event) {
    var $fields = $(".focusOnEnter");
    var index = $fields.index(this);

    switch (event.keyCode) {
        case 13: // Enter
            event.preventDefault();
            focusField($fields[index + 1]);
            break;
        case 37: // Left arrow
            event.preventDefault();
            focusField($fields[index - 1]);
            break;
        case 39: // Right arrow
            event.preventDefault();
            focusField($fields[index + 1]);
            break;
    }

    function focusField(field) {
        if (!field) return;
        var $field = $(field);
        $field.focus();
        if ($field.is("input[type='text']")) {
            $field.select();
        }
    }
});

function validateHhMm(inputField) {
    if (inputField.value != '') {
        var isValid = /^([0-1]?[0-9]|2[0-4]):([0-5][0-9])(:[0-5][0-9])?$/.test(inputField.value);
        if (isValid) {
            inputField.style.backgroundColor = 'transparent';
        } else {
            inputField.style.backgroundColor = '#fba';
            inputField.focus();
            inputField.select();
        }
        return isValid;
    }
}
//function Enterdate() {
//    $('.EnterDate').keyup(function (e) {

//        var key = String.fromCharCode(e.keyCode);

//        if (!(key >= 0 && key <= 9)) $(this).val($(this).val().substr(0, $(this).val().length - 1));

//        var value = $(this).val();

//        if (value.length == 2 || value.length == 5) $(this).val($(this).val() + '-');

//    });
//}
//FOR DATE VALIDATION
var specialKeys = new Array();
specialKeys.push(8); //Backspace
function IsNumeric(e) {
    var keyCode = e.which ? e.which : e.keyCode
    var ret = ((keyCode >= 48 && keyCode <= 57 || keyCode == 45) || specialKeys.indexOf(keyCode) != -1);
    //document.getElementById("error").style.display = ret ? "none" : "inline";
    return ret;
}

//FOR TIME VALIDATION
var timeKeys = new Array();
timeKeys.push(8); //Backspace
function IsTime(e) {
    var keyCode = e.which ? e.which : e.keyCode
    var ret = ((keyCode >= 48 && keyCode <= 57 || keyCode == 58) || specialKeys.indexOf(keyCode) != -1);
    //document.getElementById("error").style.display = ret ? "none" : "inline";
    return ret;
}
//FOR ONLY NUMERIC VALIDATION
function numeric(evt) {
    var charCode = (evt.which) ? evt.which : event.keyCode
    if (charCode > 31 && ((charCode >= 48 && charCode <= 57)))
        return true;
    else {
        //alert('Please Enter Numeric values.');
        return false;
    }
}
// FOR AMOUNT VALIDATION
var specialKeys = new Array();
specialKeys.push(8); // Backspace

function ValidAmount(evt, obj) {
    var charCode = (evt.which) ? evt.which : evt.keyCode;
    var value = obj.value;
    if (value.indexOf(".") != -1 && charCode == 46) {
        return false;
    }
    if (charCode == 46) {
        return true;
    }
    if (charCode > 31 && (charCode < 48 || charCode > 57)) {
        return false;
    }

    // ✅ Extra Step: Allow only 3 digits after decimal
    if (value.indexOf(".") != -1) {
        var decimalPart = value.split(".")[1];
        if (decimalPart && decimalPart.length >= 3) {
           // return false;
        }
    }

    return true;
}

function autocopleteOff() {
    $('input[type=text]').each(function () {
        this.setAttribute("autocomplete", "off");
    });
}
$('input[type=text]').each(function () {
    this.setAttribute("autocomplete", "off");
});
let selectionMode = false;
let selectedRow = 0;
let activeRows = [];

$("tr").on("click", function () {
    const currentTableID = $(this).closest("table").attr("id");
    const tbody = document.getElementById(currentTableID).getElementsByTagName("tbody")[0];
    const rows = tbody?.rows;

    if (!rows || rows.length === 0) return;

    // Enable selection mode
    selectionMode = true;
    activeRows = rows;

    // Mark the clicked row as selected
    for (let i = 0; i < rows.length; i++) {
        rows[i].classList.remove("table-warning");
        if (rows[i] === this) {
            selectedRow = i;
        }
    }

    rows[selectedRow].classList.add("table-warning");
});

document.body.addEventListener("keydown", function (e) {
    if (!selectionMode || !activeRows || activeRows.length === 0) return;
    if (e.key !== "ArrowUp" && e.key !== "ArrowDown") return;

    e.preventDefault();

    // Clear current highlight
    for (let i = 0; i < activeRows.length; i++) {
        activeRows[i].classList.remove("table-warning");
    }

    // Update selectedRow
    if (e.key === "ArrowUp") {
        selectedRow--;
    } else if (e.key === "ArrowDown") {
        selectedRow++;
    }

    // Wrap around
    if (selectedRow >= activeRows.length) selectedRow = 0;
    if (selectedRow < 0) selectedRow = activeRows.length - 1;

    // Highlight new row
    const row = activeRows[selectedRow];
    row.classList.add("table-warning");
    row.scrollIntoView({ behavior: "smooth", block: "center" });
});

function numericvat(evt, element) {
    var charCode = evt.which ? evt.which : evt.keyCode;

    // Allow backspace, delete, arrow keys
    if (charCode === 8 || charCode === 46 || charCode === 37 || charCode === 39) {
        return true;
    }

    // Allow numbers
    if (charCode >= 48 && charCode <= 57) {
        return true;
    }

    // Allow only one decimal point
    if (charCode === 46) {
        // If the input already has a decimal, block another
        if (element.value.indexOf('.') !== -1) {
            return false;
        }
        return true;
    }

    return false;
}


