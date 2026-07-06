package main

import "C"

import (
	shared "github.com/Froststrap/Froststrap/backend/src/shared/notification"
)

func SendNativeNotification(cTitle *C.char, cMessage *C.char) C.int {
	title := C.GoString(cTitle)
	message := C.GoString(cMessage)

	err := shared.Send(title, message)
	if err != nil {
		return 0
	}
	return 1
}

func main() {}
